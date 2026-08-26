using System.Text.RegularExpressions;

namespace SportFrog.Domain.ValueObjects;

/// <summary>
/// A segment of a public address.
/// </summary>
/// <remarks>
/// Shared because two things now need it and they have to agree. A public
/// address is built from an organization slug and a competition slug, one
/// after the other; rules that held for the first segment and not the second
/// would produce addresses that work in one half and break in the other.
///
/// Restricted to what reads and travels well in a URL: lowercase letters,
/// digits, and single hyphens between them. No leading, trailing or doubled
/// hyphen, because those survive a copy-paste badly and read as a typo.
/// </remarks>
public static partial class Slug
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 63;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern { get; }

    /// <summary>
    /// The form a slug is stored and compared in.
    /// </summary>
    /// <remarks>
    /// Applied before validating and before writing, so that what was checked
    /// is what is kept. The columns are citext and compare case-insensitively
    /// anyway; lowercasing here means the address that comes back is the one
    /// the caller will see everywhere else.
    /// </remarks>
    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool IsAcceptable(string slug) =>
        slug.Length is >= MinimumLength and <= MaximumLength
        && Pattern.IsMatch(slug);

    /// <summary>
    /// The refusal, worded once so both slugs are rejected the same way.
    /// </summary>
    public static string Requirement =>
        $"The address must be between {MinimumLength} and {MaximumLength} characters, and may " +
        "contain only lowercase letters, digits and single hyphens between them.";
}
