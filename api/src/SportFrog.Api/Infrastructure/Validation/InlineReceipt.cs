namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// A payment's proof, sent inline as a data URL — a photograph of a
/// receipt, or a PDF of one.
/// </summary>
/// <remarks>
/// Built on top of <see cref="InlinePhoto"/> rather than beside it: a
/// receipt that happens to be a photograph is exactly a photograph, same
/// three formats and same size ceiling, and repeating that list here would
/// leave two places that have to agree on what counts as one. The only
/// thing a receipt may be that a photograph may not is a PDF — a bank app's
/// own export, most often — which is why this exists as its own type
/// instead of widening <see cref="InlinePhoto"/> itself: nothing else that
/// calls <c>InlinePhoto.IsAcceptable</c> — creating an athlete, a club, an
/// organization — should start accepting a PDF because a payment feature
/// needed one.
///
/// <see cref="InlinePhoto.TryRead"/> is reused unmodified for decoding:
/// it only pulls the base64 payload out from after the comma and never
/// looks at the declared type, so it already works for a PDF exactly as
/// it works for an image.
/// </remarks>
public static class InlineReceipt
{
    private const int MaximumLength = 8 * 1024 * 1024;

    private const string PdfPrefix = "data:application/pdf";

    /// <summary>
    /// Whether a value that was given is a receipt this accepts. An absent
    /// one is acceptable: whether the field may be left out is the field's
    /// question, not this one's — same reasoning as <see cref="InlinePhoto.IsAcceptable"/>.
    /// </summary>
    public static bool IsAcceptable(string? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.StartsWith(PdfPrefix, StringComparison.OrdinalIgnoreCase)
            ? value.Length <= MaximumLength && HasBase64Payload(value)
            : InlinePhoto.IsAcceptable(value);
    }

    public static string Requirement =>
        $"El comprobante tiene que ser una foto (PNG, JPEG o WebP) o un PDF, de menos de " +
        $"{MaximumLength / (1024 * 1024)} MB.";

    /// <summary>
    /// Whether a value this accepted announces itself as a PDF rather than
    /// an image — <see cref="PaymentReceipt"/>'s own cue for whether to
    /// normalize the decoded bytes or store them exactly as they arrived.
    /// </summary>
    public static bool IsPdf(string value) =>
        value.StartsWith(PdfPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the bytes out of an accepted receipt. Delegates to
    /// <see cref="InlinePhoto.TryRead"/> unmodified — see this type's own
    /// remarks for why that already works for a PDF.
    /// </summary>
    public static bool TryRead(string value, out byte[] content) =>
        InlinePhoto.TryRead(value, out content);

    /// <summary>The same shape check <see cref="InlinePhoto.TryRead"/> already makes, without decoding anything.</summary>
    private static bool HasBase64Payload(string value)
    {
        var separator = value.IndexOf(',', StringComparison.Ordinal);

        return separator >= 0
            && value[..separator].EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
    }
}
