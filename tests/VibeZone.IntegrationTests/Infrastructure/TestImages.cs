using SkiaSharp;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>Real cover images generated in memory (no binary fixtures in the repository).</summary>
public static class TestImages
{
    public static byte[] Cover(int width = 900, int height = 700, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [SKColors.Gold, SKColors.Teal], SKShaderTileMode.Clamp) };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>Width and height of an encoded image.</summary>
    public static (int Width, int Height) Size(byte[] image)
    {
        using var codec = SKCodec.Create(new MemoryStream(image));
        return (codec.Info.Width, codec.Info.Height);
    }
}
