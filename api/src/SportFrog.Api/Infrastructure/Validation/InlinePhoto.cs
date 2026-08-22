namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// A photograph sent inline, as a data URL.
/// </summary>
/// <remarks>
/// Shared rather than written at each call site: creating and correcting an
/// athlete accept the same thing, and two copies of "what counts as a photo"
/// drift into disagreeing about it — one of them will end up accepting a
/// format the other refuses, and which endpoint you used will decide whether
/// your photo saved.
///
/// Only the shape is checked here: that it announces itself as one of three
/// image types and is not absurdly large. Nothing decodes it, so this is not
/// a guarantee that the bytes are an image — it is a guard against a text
/// column quietly filling up with something else.
///
/// The size is measured on the text, and base64 costs about a third more than
/// the bytes it carries, so this ceiling is roughly a four-megabyte image.
/// </remarks>
public static class InlinePhoto
{
    private const int MaximumLength = 5 * 1024 * 1024;

    private const string Prefix = "data:image/";

    private static readonly string[] SupportedTypes = ["image/png", "image/jpeg", "image/webp"];

    /// <summary>
    /// Whether a value that was given is a photo this accepts. An absent one
    /// is acceptable: whether the field may be left out is the field's
    /// question, not this one's.
    /// </summary>
    public static bool IsAcceptable(string? value)
    {
        if (value is null)
        {
            return true;
        }

        if (!value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || value.Length > MaximumLength)
        {
            return false;
        }

        // Everything before the comma describes the payload; everything after
        // it is the payload. Without the comma there is no payload at all.
        var separator = value.IndexOf(',', StringComparison.Ordinal);
        if (separator < 0)
        {
            return false;
        }

        var header = value[..separator];

        return SupportedTypes.Any(type =>
            header.Contains(type, StringComparison.OrdinalIgnoreCase));
    }

    public static string Requirement =>
        $"The photo must be a PNG, JPEG or WebP data URL under {MaximumLength / (1024 * 1024)} MB.";
}
