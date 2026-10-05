using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using SkiaSharp;

namespace QrMenu.Infrastructure.Files;

/// <summary>SkiaSharp (MIT) based: the same imaging engine Chrome and Android use.</summary>
public class ImageOptimizer : IImageOptimizer
{
    // Longest side kept for each use. About twice the largest size it is shown at, so it stays sharp on
    // high-density phone screens; anything bigger only costs download time and phone memory.
    private static int MaxSide(ImagePurpose purpose) => purpose switch
    {
        ImagePurpose.Background => 1920,
        ImagePurpose.Logo => 512,
        _ => 1200
    };

    // 85 looks the same as the original to the eye, at a fraction of the size of a camera photo.
    private const int JpegQuality = 85;

    // Refuse decompression bombs: a tiny file that claims to be a gigantic picture.
    private const long MaxPixels = 60_000_000;

    private static readonly SKSamplingOptions HighQuality = new(SKCubicResampler.Mitchell);

    public Task<OptimizedImage> OptimizeAsync(Stream source, ImagePurpose purpose, CancellationToken ct = default)
    {
        // Skia only reads what it recognises as an image, so a renamed text or script file is refused here.
        using var codec = SKCodec.Create(source)
            ?? throw new ConflictException("This file is not a valid JPEG, PNG or WEBP image.");

        var format = codec.EncodedFormat;
        if (format is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            throw new ConflictException("Only JPEG, PNG or WEBP images are allowed.");
        }

        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            throw new ConflictException("This image is too large. Please upload a photo under 60 megapixels.");
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new ConflictException("This image could not be read. Please try another photo.");
        }

        ct.ThrowIfCancellationRequested();

        // Phones store photos sideways plus a "rotate me" flag; bake the rotation into the pixels.
        using var upright = ApplyOrientation(decoded, codec.EncodedOrigin);

        var (width, height) = FitWithin(upright.Width, upright.Height, MaxSide(purpose));
        using var resized = width == upright.Width && height == upright.Height
            ? upright.Copy()
            : upright.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), HighQuality);

        // Re-encoding writes only the pixels: GPS location, camera serial number and other hidden data are dropped.
        using var image = SKImage.FromBitmap(resized);
        var hasTransparency = codec.Info.AlphaType != SKAlphaType.Opaque && HasTransparency(resized);

        if (hasTransparency)
        {
            // Logos with a see-through background stay PNG so the transparency is kept.
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return Task.FromResult(new OptimizedImage(png.ToArray(), ".png", "image/png", width, height));
        }

        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return Task.FromResult(new OptimizedImage(jpeg.ToArray(), ".jpg", "image/jpeg", width, height));
    }

    private static (int Width, int Height) FitWithin(int width, int height, int maxSide)
    {
        if (width <= maxSide && height <= maxSide)
        {
            return (width, height); // never enlarge a small image
        }

        var scale = (double)maxSide / Math.Max(width, height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var rotated = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));

        using var canvas = new SKCanvas(rotated);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirrored
                canvas.Scale(-1, 1, width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight: // 180°
                canvas.RotateDegrees(180, width / 2f, height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft: // flipped vertically
                canvas.Scale(1, -1, 0, height / 2f);
                break;
            case SKEncodedOrigin.LeftTop: // transposed
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // 90° clockwise (most portrait phone photos)
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // transversed: (x, y) -> (width - y, height - x)
                canvas.SetMatrix(new SKMatrix(0, -1, width, -1, 0, height, 0, 0, 1));
                break;
            case SKEncodedOrigin.LeftBottom: // 90° counter-clockwise
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        using var sourceImage = SKImage.FromBitmap(source);
        canvas.DrawImage(sourceImage, 0, 0, HighQuality);
        return rotated;
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        // Rgba8888: every 4th byte is alpha.
        var pixels = bitmap.GetPixelSpan();
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] < 250)
            {
                return true;
            }
        }
        return false;
    }
}
