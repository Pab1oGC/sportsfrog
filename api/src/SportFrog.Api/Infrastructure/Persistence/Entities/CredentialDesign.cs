namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// The values an organization chooses for its credentials, once, for all of
/// its competitions: the legal notice, the background, the accent colour and
/// a fallback logo.
/// </summary>
/// <remarks>
/// The card's structure is not here: it is fixed by decree, so there is
/// nothing to design there. A competition names one of these rows, and a
/// competition that names none prints from the organization's default.
///
/// Deleting one sends its competitions back to the default rather than
/// deleting them, because the design is a choice a competition made, not a
/// fact the competition cannot exist without. Credentials already printed are
/// unaffected: each batch froze its own values when it was requested.
/// </remarks>
public sealed class CredentialDesign
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public required string Name { get; set; }

    /// <summary>The notice on the back. Null uses the decree's default text.</summary>
    public string? LegalText { get; set; }

    /// <summary>Storage key of the faint picture drawn across the sheet.</summary>
    public string? BackgroundKey { get; set; }

    /// <summary>The accent colour, as "#rrggbb". Null uses the competition's portal accent.</summary>
    public string? AccentColorHex { get; set; }

    /// <summary>Storage key of the logo used when a competition has none of its own.</summary>
    public string? LogoKey { get; set; }

    /// <summary>
    /// The one a competition without its own choice prints from. At most one
    /// per organization, kept by the application rather than an index, so an
    /// organization can have no design at all before it makes one.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
