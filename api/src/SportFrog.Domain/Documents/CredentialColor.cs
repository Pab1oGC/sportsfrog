using System.Globalization;

namespace SportFrog.Domain.Documents;

/// <summary>
/// What text colour reads over a background an operator chose.
/// </summary>
/// <remarks>
/// A category's colour and a zone's colour are picked by whoever sets up the
/// competition, not by this system, and nothing stops that choice from being
/// a pale yellow instead of the dark blues every example so far happens to
/// use. White text printed on a light background is unreadable rather than
/// merely unattractive — on a card a volunteer has a few seconds to check at
/// a gate, that is a real failure, not a style preference — so the colour of
/// the text has to follow the colour of the box rather than being fixed to
/// white the way a mock-up with one dark colour in mind might suggest.
/// </remarks>
public static class CredentialColor
{
    private const string Light = "#FFFFFF";

    private const string Dark = "#1A1A1A";

    /// <summary>
    /// Black or white, whichever reads better on <paramref name="backgroundHex"/>.
    /// </summary>
    /// <remarks>
    /// Perceived brightness (the YIQ formula), not the fuller WCAG contrast
    /// ratio: this is choosing between exactly two colours for a card nobody
    /// reads in the dark, not certifying accessibility compliance, and the
    /// simpler formula is the one worth being easy to verify by hand.
    /// </remarks>
    public static string ForegroundFor(string backgroundHex)
    {
        var (red, green, blue) = ParseHex(backgroundHex);
        var brightness = ((red * 299) + (green * 587) + (blue * 114)) / 1000d;

        return brightness >= 128 ? Dark : Light;
    }

    private static (int Red, int Green, int Blue) ParseHex(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');

        return (
            int.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
