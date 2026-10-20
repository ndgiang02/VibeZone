using SkiaSharp;
using VibeZone.Modules.Media.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Infrastructure;

/// <summary>
/// Uploaded images with Skia: signature allow-list (JPEG/PNG/WebP — no SVG, GIF, HEIC…) checked before the
/// decoder runs, dimension limits checked from the header before decoding, then EXIF orientation applied,
/// a center crop to the spec's aspect ratio, sRGB, alpha flattened, and two JPEG renditions encoded from
/// scratch so no source metadata (EXIF/GPS, XMP, comments) survives. Never upscales.
/// </summary>
internal sealed class SkiaArtworkProcessor : IArtworkProcessor
{
    private const int JpegQuality = 86;

    private static readonly DomainError Invalid = DomainError.Validation("artwork_invalid", "Use a JPEG, PNG or WebP image.");

    /// <summary>Track cover (square 320/1200 px).</summary>
    public Result<ArtworkRenditions> Render(ReadOnlySpan<byte> image, int minSide, long maxPixels) =>
        Render(image, ImageSpec.Cover(minSide), maxPixels);

    public Result<ArtworkRenditions> Render(ReadOnlySpan<byte> image, ImageSpec spec, long maxPixels)
    {
        if (!HasAllowedSignature(image))
        {
            return Invalid;
        }

        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            return Invalid;
        }

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width <= 0 || height <= 0)
        {
            return Invalid;
        }

        if ((long)width * height > maxPixels)
        {
            return DomainError.Validation("artwork_too_large", "The image has too many pixels.");
        }

        var origin = codec.EncodedOrigin;
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (uprightWidth, uprightHeight) = swapped ? (height, width) : (width, height);
        if (uprightWidth < spec.MinWidth || uprightHeight < spec.MinHeight)
        {
            return DomainError.Validation("artwork_too_small", $"Images must be at least {spec.MinWidth} × {spec.MinHeight} pixels.");
        }

        // Width of the largest crop of the upright image; decode only as large as the large rendition needs.
        var cropWidth = CropSize(uprightWidth, uprightHeight, spec).Width;
        using var decoded = Decode(codec, Math.Min(1f, (float)spec.LargeWidth / cropWidth));
        if (decoded is null)
        {
            return Invalid;
        }

        using var upright = Orient(decoded, origin);
        var crop = CropSize(upright.Width, upright.Height, spec);
        var source = SKRectI.Create((upright.Width - crop.Width) / 2, (upright.Height - crop.Height) / 2, crop.Width, crop.Height);
        using var sourceImage = SKImage.FromBitmap(upright);
        return new ArtworkRenditions(
            Encode(sourceImage, source, Math.Min(spec.SmallWidth, crop.Width), spec),
            Encode(sourceImage, source, Math.Min(spec.LargeWidth, crop.Width), spec));
    }

    internal static bool HasAllowedSignature(ReadOnlySpan<byte> b) =>
        (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||
        b.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ||
        (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8));

    /// <summary>Largest rectangle of the spec's aspect ratio that fits in width × height.</summary>
    private static SKSizeI CropSize(int width, int height, ImageSpec spec)
    {
        var widthForFullHeight = (long)height * spec.AspectWidth / spec.AspectHeight;
        return widthForFullHeight <= width
            ? new SKSizeI((int)widthForFullHeight, height)
            : new SKSizeI(width, (int)((long)width * spec.AspectHeight / spec.AspectWidth));
    }

    /// <summary>
    /// Decodes into sRGB, letting the codec downscale (JPEG DCT scaling) when it still leaves at least
    /// <paramref name="scale"/> of the original — a 40 MP photo then never needs 160 MB of pixels. Null if the
    /// data is corrupt or truncated.
    /// </summary>
    private static SKBitmap? Decode(SKCodec codec, float scale)
    {
        var size = codec.Info.Size;
        if (scale < 1f)
        {
            var scaled = codec.GetScaledDimensions(scale);
            if (scaled.Width >= Math.Ceiling(size.Width * scale) && scaled.Height >= Math.Ceiling(size.Height * scale))
            {
                size = scaled;
            }
        }

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            return null;
        }

        return bitmap;
    }

    /// <summary>Applies the EXIF orientation, returning an upright copy (dimensions swap for 90° rotations).</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var matrix = Orientation(origin, source.Width, source.Height);
        var swapped = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var info = source.Info.WithSize(swapped ? source.Height : source.Width, swapped ? source.Width : source.Height);
        var upright = new SKBitmap(info);
        using var canvas = new SKCanvas(upright);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest)); // a pure pixel permutation
        canvas.Flush();
        return upright;
    }

    private static byte[] Encode(SKImage source, SKRectI crop, int width, ImageSpec spec)
    {
        var height = Math.Max(1, (int)Math.Round((double)width * spec.AspectHeight / spec.AspectWidth));
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black); // JPEG has no alpha; transparent images sit on the app's dark background
        using var paint = new SKPaint();
        canvas.DrawImage(source, SKRect.Create(crop.Left, crop.Top, crop.Width, crop.Height), SKRect.Create(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.Flush();
        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }

    /// <summary>Maps stored pixels (w × h) to display orientation (EXIF 1–8).</summary>
    internal static SKMatrix Orientation(SKEncodedOrigin origin, float w, float h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}
