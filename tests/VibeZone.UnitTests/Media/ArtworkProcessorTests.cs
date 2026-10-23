using System.Text;
using SkiaSharp;
using VibeZone.Modules.Media.Infrastructure;

namespace VibeZone.UnitTests.Media;

public sealed class ArtworkProcessorTests
{
    private readonly SkiaArtworkProcessor _processor = new();

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Renders_square_jpeg_renditions_without_upscaling(SKEncodedImageFormat format)
    {
        var result = _processor.Render(Image(800, 600, format), minSide: 200, maxPixels: 40_000_000);

        Assert.True(result.IsSuccess, result.Error?.Code);
        AssertJpeg(result.Value.Small, 320);
        AssertJpeg(result.Value.Large, 600); // the short side; never upscaled to 1200
    }

    [Fact]
    public void Large_images_are_capped_at_the_large_size()
    {
        var result = _processor.Render(Image(2400, 3000, SKEncodedImageFormat.Jpeg), 200, 40_000_000);

        Assert.True(result.IsSuccess, result.Error?.Code);
        AssertJpeg(result.Value.Large, 1200);
    }

    [Fact]
    public void Rejects_images_below_the_minimum_side()
    {
        Assert.Equal("artwork_too_small", _processor.Render(Image(400, 150, SKEncodedImageFormat.Png), 200, 40_000_000).Error?.Code);
    }

    [Fact]
    public void Rejects_images_over_the_pixel_limit_before_decoding()
    {
        Assert.Equal("artwork_too_large", _processor.Render(Image(1000, 1000, SKEncodedImageFormat.Png), 200, 999_999).Error?.Code);
    }

    [Theory]
    [InlineData("GIF89a\u0001\u0000\u0001\u0000")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("")]
    public void Rejects_formats_outside_the_allow_list(string content)
    {
        Assert.Equal("artwork_invalid", _processor.Render(Encoding.ASCII.GetBytes(content), 200, 40_000_000).Error?.Code);
    }

    [Fact]
    public void Rejects_truncated_and_corrupt_images()
    {
        var jpeg = Image(800, 800, SKEncodedImageFormat.Jpeg);

        Assert.Equal("artwork_invalid", _processor.Render(jpeg.AsSpan(0, jpeg.Length / 3), 200, 40_000_000).Error?.Code);
        Assert.Equal("artwork_invalid", _processor.Render([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5], 200, 40_000_000).Error?.Code);
    }

    [Fact]
    public void Drops_exif_metadata_but_applies_its_orientation()
    {
        // Left half red, right half blue, tagged "rotate 90° clockwise" (EXIF orientation 6) plus a GPS-ish marker.
        var jpeg = WithExifOrientation(SplitImage(400), orientation: 6, extra: "GPSLatitude 21.0285N");

        var result = _processor.Render(jpeg, 200, 40_000_000);

        Assert.True(result.IsSuccess, result.Error?.Code);
        var large = result.Value.Large;
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(large), StringComparison.Ordinal);
        Assert.DoesNotContain("GPS", Encoding.ASCII.GetString(large), StringComparison.Ordinal);
        using var output = SKBitmap.Decode(large);
        // Rotated clockwise, the left (red) half is now on top.
        AssertMostly(output.GetPixel(200, 60), red: true);
        AssertMostly(output.GetPixel(200, 340), red: false);
    }

    private static void AssertJpeg(byte[] bytes, int side)
    {
        Assert.True(bytes is [0xFF, 0xD8, 0xFF, ..]);
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(side, codec.Info.Width);
        Assert.Equal(side, codec.Info.Height);
    }

    private static void AssertMostly(SKColor color, bool red)
    {
        Assert.True(red ? color.Red > 200 && color.Blue < 60 : color.Blue > 200 && color.Red < 60, $"Unexpected colour {color}");
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [SKColors.OrangeRed, SKColors.MediumPurple], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static byte[] SplitImage(int side)
    {
        using var bitmap = new SKBitmap(side, side);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, side / 2f, side, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    /// <summary>Inserts a minimal big-endian EXIF APP1 segment (IFD0 with Orientation and an ASCII tag) after SOI.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation, string extra)
    {
        var text = Encoding.ASCII.GetBytes(extra + "\0");
        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 0x2A, 0, 0, 0, 8 };
        tiff.AddRange([0, 2]); // two entries
        tiff.AddRange([0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0]); // Orientation, SHORT
        var textOffset = 8 + 2 + (2 * 12) + 4;
        tiff.AddRange([0x01, 0x0E, 0, 2, 0, 0, 0, (byte)text.Length, 0, 0, 0, (byte)textOffset]); // ImageDescription, ASCII
        tiff.AddRange([0, 0, 0, 0]); // no next IFD
        tiff.AddRange(text);

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray();
    }
}
