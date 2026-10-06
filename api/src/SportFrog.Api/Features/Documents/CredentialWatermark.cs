using SkiaSharp;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Turns a background picture into a faint one, meant to sit behind a
/// credential's text without competing with it.
/// </summary>
/// <remarks>
/// Faded against white rather than given transparency. The card's paper is
/// white, so blending onto white is what the eye sees anyway, and an opaque
/// JPEG is smaller and prints the same on every printer, where a transparent
/// picture is not guaranteed to.
///
/// Done once per batch, the same as the competition's logo: every credential
/// of the batch shares one faded copy rather than fading it four hundred times.
/// </remarks>
internal static class CredentialWatermark
{
    /// <summary>
    /// The share of the picture that stays visible. Low enough that the name,
    /// the codes and the legal notice remain the things read at a gate.
    /// </summary>
    public const float Strength = 0.12f;

    private const int Quality = 90;

    /// <summary>The faded picture as a JPEG, or null if the bytes are not an image.</summary>
    public static byte[]? Fade(byte[]? image)
    {
        if (image is null)
        {
            return null;
        }

        // FromEncodedData answers null for bytes that are not an image; the
        // bitmap decoder throws instead, which would turn a bad upload into a
        // failed batch.
        using var decoded = SKImage.FromEncodedData(image);

        if (decoded is null)
        {
            return null;
        }

        var info = new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        // The paint's alpha is what fades the picture: drawing with it leaves
        // the white underneath showing through the remainder.
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Strength)),
            IsAntialias = true,
        };

        canvas.DrawImage(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, Quality);

        return encoded.ToArray();
    }
}
