using SkiaSharp;

namespace SportFrog.Api.Infrastructure.Storage;

/// <summary>
/// A face to draw when a person does not have one on file.
/// </summary>
/// <remarks>
/// A credential batch used to skip anybody without a photograph outright —
/// reasonable when the alternative was a hole where the face goes, but it
/// meant one missing upload for a twelve-year-old held up every other card in
/// the batch too, and a coach chasing down a photo before anyone on the team
/// could be credentialed. A generic silhouette is the better trade: whoever
/// is reading still sees who is missing a real photograph (the placeholder
/// looks obviously like one, and nothing here hides that), but a credential
/// batch finishes and a player's own report still renders without either
/// being held hostage by a photograph nobody has sent yet — see
/// <see cref="Features.Reports.AthleteReportQuery"/> and
/// <see cref="Features.Documents.IssueDocumentsJob"/>, the two places that
/// answer "what photo do I draw for this person" and agree on this same
/// placeholder for the same reason.
///
/// Drawn once, in code, rather than shipped as an image file: a placeholder
/// that looks the same for every organization needs no upload, no
/// object-storage key, and nothing that can go missing from a bucket the way
/// an actual photograph can. The shape is the same head-and-shoulders
/// silhouette most messaging apps default a contact to — recognisable as
/// "nobody has uploaded one yet" rather than as a rendering error.
/// </remarks>
internal static class DefaultAvatar
{
    private const int Size = 480;

    private static readonly SKColor FieldColor = new(0xCF, 0xD8, 0xDC);
    private static readonly SKColor SilhouetteColor = new(0xB0, 0xBE, 0xC5);

    private static readonly Lazy<byte[]> Cached = new(Draw);

    /// <summary>The placeholder, encoded once and reused for every reader that needs it.</summary>
    public static byte[] Bytes => Cached.Value;

    private static byte[] Draw()
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(FieldColor);

        using var silhouette = new SKPaint { Color = SilhouetteColor, IsAntialias = true };
        var center = Size / 2f;

        // The head, sat in the upper third of the square.
        canvas.DrawCircle(center, Size * 0.38f, Size * 0.16f, silhouette);

        // The shoulders: the top of a much wider circle, clipped so only its
        // upper curve shows — otherwise it reads as a second, bigger head
        // rather than shoulders.
        canvas.Save();
        canvas.ClipRect(new SKRect(0, Size * 0.62f, Size, Size));
        canvas.DrawCircle(center, Size * 0.95f, Size * 0.42f, silhouette);
        canvas.Restore();

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        return encoded.ToArray();
    }
}
