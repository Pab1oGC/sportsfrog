using System.ComponentModel.DataAnnotations;
using QRCoder;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Where a scanned credential sends whoever scanned it.
/// </summary>
/// <remarks>
/// Required and validated on start, like the signing key, and for a reason
/// this setting has that most do not: it is printed. A wrong address in a
/// configuration file is a deployment somebody fixes; a wrong address on four
/// hundred laminated cards is four hundred cards. Refusing to start is the
/// cheapest moment to find out.
/// </remarks>
public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    /// <summary>
    /// The page a QR code leads to, with the organization and the serial
    /// appended.
    /// </summary>
    /// <remarks>
    /// A full address rather than a bare code, because what scans it is the
    /// camera app on a parent's phone. A code would show them a string of
    /// characters and leave them to work out where to type it.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string VerificationBaseUrl { get; set; } = string.Empty;
}

/// <summary>Draws the code that gets scanned.</summary>
internal static class VerificationCode
{
    /// <summary>The address printed as a QR code on a document.</summary>
    public static string Address(string baseUrl, string organizationSlug, string serial) =>
        $"{baseUrl.TrimEnd('/')}/{organizationSlug}/{Uri.EscapeDataString(serial)}";

    /// <summary>
    /// The QR itself, as a PNG.
    /// </summary>
    /// <remarks>
    /// Quartile correction, which tolerates a quarter of the code being
    /// unreadable. That is not paranoia about printing: this is going onto a
    /// card that lives in a pocket for a season, gets rained on, and is
    /// scanned in a sports hall by a phone held at an angle.
    ///
    /// Drawn at a fixed module size and scaled by the layout, so the code is
    /// crisp whatever box the designer drew — a QR resampled from too few
    /// pixels is a QR that will not scan.
    /// </remarks>
    public static byte[] Draw(string address)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(address, QRCodeGenerator.ECCLevel.Q);

        return new PngByteQRCode(data).GetGraphic(pixelsPerModule: 12);
    }
}
