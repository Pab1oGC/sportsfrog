using SportFrog.Domain.Competitions;

namespace SportFrog.Domain.Documents;

/// <summary>
/// The values an organization stored in one of its credential designs.
/// </summary>
/// <param name="LegalText">The notice on the back. Null or blank uses <see cref="CredentialDesignResolver.DefaultLegalText"/>.</param>
/// <param name="BackgroundKey">The faint picture drawn across the sheet.</param>
/// <param name="AccentColorHex">The accent colour, as "#rrggbb".</param>
/// <param name="LogoKey">The logo for competitions that have none of their own.</param>
public sealed record CredentialDesignValues(
    string? LegalText,
    string? BackgroundKey,
    string? AccentColorHex,
    string? LogoKey);

/// <summary>
/// What a credential prints, once a competition and the design it uses are
/// combined. Every field here is a fact; nothing downstream looks anything up.
/// </summary>
public sealed record CredentialDesignResolution(
    string LegalText,
    string? LogoKey,
    string? BackgroundKey,
    string? AccentColorHex);

/// <summary>
/// Combines a competition with the design it prints from.
/// </summary>
/// <remarks>
/// Each field has one owner. The competition owns its own logo, because a
/// logo identifies the competition; the design owns everything else, because
/// those values are the organization's and repeat across its competitions.
///
/// Where a value is missing, the answer is the one a credential gave before
/// designs existed, so a competition that never chose a design prints exactly
/// what it printed yesterday. The background falls back to the competition's
/// own banner, and the accent to the portal's accent: the same pictures and
/// colour the rest of the competition already shows.
/// </remarks>
public static class CredentialDesignResolver
{
    /// <summary>
    /// What prints when no notice has been written anywhere — generic enough to
    /// be true of any competition, specific enough that a credential is never
    /// issued with the back of the card blank.
    /// </summary>
    public const string DefaultLegalText =
        "Esta credencial es propiedad de la organización que la emite y puede ser retirada en " +
        "cualquier momento. Es personal e intransferible, y debe portarse en un lugar visible " +
        "durante toda la competencia. Su uso implica la aceptación de ser identificado, " +
        "fotografiado y filmado con fines de organización y seguridad. La pérdida o el deterioro " +
        "deben reportarse de inmediato a la organización.";

    public static CredentialDesignResolution Resolve(PublicSettings? publicSettings, CredentialDesignValues? design)
    {
        var legalText = design?.LegalText?.Trim();

        return new CredentialDesignResolution(
            LegalText: string.IsNullOrWhiteSpace(legalText) ? DefaultLegalText : legalText,
            LogoKey: publicSettings?.LogoKey ?? design?.LogoKey,
            BackgroundKey: design?.BackgroundKey ?? publicSettings?.BannerKey,
            AccentColorHex: design?.AccentColorHex ?? PortalTheme.Resolve(publicSettings)?.Primary);
    }
}
