using SkiaSharp;
using SportFrog.Api.Features.Documents;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// The background is faded toward white: lighter than the picture it came
/// from, the same size, and refused when the bytes are not an image at all.
/// </summary>
public sealed class CredentialWatermarkTests
{
    private static byte[] SolidBlackPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Black);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }

    [Fact]
    public void Fade_ReturnsAnImageOfTheSameSize()
    {
        var faded = CredentialWatermark.Fade(SolidBlackPng(40, 30));

        Assert.NotNull(faded);

        using var decoded = SKBitmap.Decode(faded);
        Assert.Equal(40, decoded.Width);
        Assert.Equal(30, decoded.Height);
    }

    [Fact]
    public void Fade_KeepsOnlyTheStrengthOfThePictureOverWhite()
    {
        var faded = CredentialWatermark.Fade(SolidBlackPng(40, 30));

        using var decoded = SKBitmap.Decode(faded);
        var centre = decoded.GetPixel(20, 15);

        // A black picture at 12% strength is 88% white underneath it. The
        // JPEG adds a little error, hence the range rather than an exact value.
        var expected = 255 * (1 - CredentialWatermark.Strength);
        Assert.InRange(centre.Red, expected - 6, expected + 6);
        Assert.InRange(centre.Green, expected - 6, expected + 6);
        Assert.InRange(centre.Blue, expected - 6, expected + 6);
    }

    [Fact]
    public void Fade_OfAWhitePicture_StaysWhite()
    {
        using var bitmap = new SKBitmap(20, 20);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        var faded = CredentialWatermark.Fade(encoded.ToArray());

        using var decoded = SKBitmap.Decode(faded);
        var centre = decoded.GetPixel(10, 10);
        Assert.InRange(centre.Red, 250, 255);
    }

    [Fact]
    public void Fade_OfNothing_IsNothing()
    {
        Assert.Null(CredentialWatermark.Fade(null));
    }

    [Fact]
    public void Fade_OfBytesThatAreNotAnImage_IsNothing()
    {
        Assert.Null(CredentialWatermark.Fade([1, 2, 3, 4, 5]));
    }
}
