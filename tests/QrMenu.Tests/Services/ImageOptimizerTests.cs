using FluentAssertions;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Infrastructure.Files;
using SkiaSharp;
using Xunit;

namespace QrMenu.Tests.Services;

public class ImageOptimizerTests
{
    private readonly ImageOptimizer _optimizer = new();

    /// <summary>A photo whose top-left quarter is red and the rest blue, so its orientation can be checked.</summary>
    private static byte[] MakeImage(int width, int height, SKEncodedImageFormat format, bool transparent = false)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(transparent ? SKColors.Transparent : SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, red);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>Adds the EXIF "Orientation" tag a phone writes, right after the JPEG start marker.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little-endian TIFF header, IFD at 8
            0x01, 0x00,                                               // 1 entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,           // tag 0x0112 Orientation, SHORT, count 1
            (byte)orientation, 0x00, 0x00, 0x00,                      // value
            0x00, 0x00, 0x00, 0x00                                    // no next IFD
        ];
        byte[] header = [.. "Exif"u8, 0, 0];
        var length = 2 + header.Length + tiff.Length;
        byte[] app1 = [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. header, .. tiff];
        return [jpeg[0], jpeg[1], .. app1, .. jpeg[2..]];
    }

    private static SKBitmap Decode(OptimizedImage image) => SKBitmap.Decode(image.Content);

    private static bool IsRed(SKColor c) => c.Red > 180 && c.Blue < 90;

    [Fact]
    public async Task HugeBackground_IsScaledToFullHd_AndMuchSmaller()
    {
        var original = MakeImage(5064, 3375, SKEncodedImageFormat.Jpeg);

        var result = await _optimizer.OptimizeAsync(new MemoryStream(original), ImagePurpose.Background);

        result.Width.Should().Be(1920);
        result.Height.Should().Be(1280);
        result.Extension.Should().Be(".jpg");
        result.Content.Length.Should().BeLessThan(original.Length);
    }

    [Theory]
    [InlineData(ImagePurpose.Item, 1200)]
    [InlineData(ImagePurpose.Logo, 512)]
    public async Task EachPurpose_HasItsOwnMaximum(ImagePurpose purpose, int max)
    {
        var result = await _optimizer.OptimizeAsync(new MemoryStream(MakeImage(3000, 2000, SKEncodedImageFormat.Jpeg)), purpose);

        Math.Max(result.Width, result.Height).Should().Be(max);
    }

    [Fact]
    public async Task SmallImage_IsNeverEnlarged()
    {
        var result = await _optimizer.OptimizeAsync(new MemoryStream(MakeImage(300, 200, SKEncodedImageFormat.Png)), ImagePurpose.Background);

        (result.Width, result.Height).Should().Be((300, 200));
        result.Extension.Should().Be(".jpg"); // opaque PNG photo saved as JPEG
    }

    [Fact]
    public async Task TransparentLogo_StaysPng()
    {
        var result = await _optimizer.OptimizeAsync(new MemoryStream(MakeImage(400, 400, SKEncodedImageFormat.Png, transparent: true)), ImagePurpose.Logo);

        result.Extension.Should().Be(".png");
        using var bitmap = Decode(result);
        bitmap.GetPixel(390, 390).Alpha.Should().Be(0);
    }

    [Fact]
    public async Task NonImageFile_IsRejected()
    {
        var fake = "<script>alert(1)</script>"u8.ToArray();

        var act = () => _optimizer.OptimizeAsync(new MemoryStream(fake), ImagePurpose.Item);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // Where the red top-left quarter of the stored pixels must end up once the phone's rotation flag is applied.
    [Theory]
    [InlineData(1, false, "TL")]
    [InlineData(2, false, "TR")]
    [InlineData(3, false, "BR")]
    [InlineData(4, false, "BL")]
    [InlineData(5, true, "TL")]
    [InlineData(6, true, "TR")]
    [InlineData(7, true, "BR")]
    [InlineData(8, true, "BL")]
    public async Task PhoneRotation_IsApplied(ushort orientation, bool swapsSides, string redCorner)
    {
        var jpeg = WithExifOrientation(MakeImage(400, 200, SKEncodedImageFormat.Jpeg), orientation);

        var result = await _optimizer.OptimizeAsync(new MemoryStream(jpeg), ImagePurpose.Item);
        using var bitmap = Decode(result);

        (bitmap.Width, bitmap.Height).Should().Be(swapsSides ? (200, 400) : (400, 200));
        var w = bitmap.Width;
        var h = bitmap.Height;
        var corners = new Dictionary<string, SKColor>
        {
            ["TL"] = bitmap.GetPixel(w / 4, h / 4),
            ["TR"] = bitmap.GetPixel(3 * w / 4, h / 4),
            ["BL"] = bitmap.GetPixel(w / 4, 3 * h / 4),
            ["BR"] = bitmap.GetPixel(3 * w / 4, 3 * h / 4)
        };
        corners.Where(c => IsRed(c.Value)).Select(c => c.Key).Should().Equal(redCorner);
    }
}
