namespace SportFrog.Domain.Documents;

/// <summary>
/// One catalogue entry as it looked the moment a batch was requested — a
/// credential's counterpart to a stored <see cref="TemplateLayout"/> version.
/// </summary>
public sealed record CredentialSnapshotItem(
    AccreditationItemKind Kind,
    string Code,
    string Name,
    string? ColorHex,
    string? IconKey);

/// <summary>One accreditation category as it looked when a batch was requested.</summary>
public sealed record CredentialSnapshotCategory(string Code, string Name, string ColorHex);

/// <summary>
/// What a credential batch prints from, frozen at the moment it was
/// requested.
/// </summary>
/// <remarks>
/// A certificate pins the template's version number on the batch that printed
/// it, so that printing it again years later reproduces the design that was
/// on screen when somebody asked for it, not whatever the design became
/// afterwards.
/// A decreed credential has no template to pin — its structure never
/// changes — but it has an accreditation catalogue an operator can still
/// rename, recolour or delete, and the organization's own legal notice,
/// which can be rewritten at any time. This is the equivalent pin: without
/// it, revoking and reissuing one card from a batch printed last season could
/// come out in this season's colours, which is exactly the drift pinning a
/// template version exists to prevent.
///
/// What is deliberately NOT frozen is who holds which category — an
/// athlete's own accreditation is read fresh every time a batch runs, the
/// same way a certificate reads the roster fresh rather than pinning who was
/// on the team when the template was designed. Only the catalogue's look is
/// a "design" in the sense that needs pinning; who it was assigned to is
/// data, and data is always current.
/// </remarks>
public sealed record CredentialSnapshot(
    string? CompetitionLogoKey,

    /// <summary>
    /// The competition's own brand colour, as "#rrggbb" — the same one
    /// already resolved for every other document this system prints (see
    /// <c>CompetitionBranding</c>). Null means the competition never set one,
    /// which every reader of it already treats as "use the system default".
    /// </summary>
    string? AccentColorHex,

    string LegalText,
    IReadOnlyDictionary<Guid, CredentialSnapshotItem> Items,
    IReadOnlyDictionary<Guid, CredentialSnapshotCategory> Categories,

    /// <summary>
    /// The faint picture drawn across the sheet, frozen with the rest. Null
    /// for a batch requested before designs had backgrounds, which prints as
    /// it always did. Defaulted so those older snapshots still read.
    /// </summary>
    string? BackgroundKey = null);
