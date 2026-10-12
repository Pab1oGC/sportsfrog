namespace SportFrog.Domain.Documents;

/// <summary>
/// One thing a credential names, ready to print — a discipline, a venue, a
/// service or a zone, resolved from the catalogue's own
/// <c>AccreditationItem</c> down to exactly what a renderer needs.
/// </summary>
/// <remarks>
/// Deliberately not the persistence entity itself: a renderer has no business
/// depending on how the catalogue is stored, and this is small enough that
/// copying the handful of fields it actually draws costs nothing.
/// </remarks>
public sealed record CredentialGrant(
    Guid ItemId,
    AccreditationItemKind Kind,
    string Code,
    string Name,
    string? ColorHex,
    string? IconKey);

/// <summary>Who the credential is for.</summary>
public sealed record CredentialSubject(
    string FullName,
    string? PhotoKey,
    string ClubName,
    string? ClubLogoKey,

    /// <summary>The person's identity document number, printed on the back beside the photograph.</summary>
    string? DocumentId = null);

/// <summary>What is true of every credential printed for the same competition.</summary>
public sealed record CredentialContext(
    string OrganizationName,
    string CompetitionName,
    string Season,
    string? CompetitionLogoKey,

    /// <summary>
    /// The competition's own brand colour, as "#rrggbb" — a light decorative
    /// touch (a box border, a detail), never anything that needs contrast
    /// checked against it the way <see cref="CredentialPrint.CategoryColorHex"/>
    /// does, because nothing is drawn on top of it. Null means draw with
    /// whatever neutral this renderer otherwise would.
    /// </summary>
    string? AccentColorHex,

    /// <summary>
    /// The legal notice on the back, written by the organization. Plain text:
    /// the card has no room for anything that was not designed for it, the
    /// same reasoning <see cref="TemplateLayout"/> gives for a certificate's
    /// layout, applied here to a field instead of to a whole column.
    /// </summary>
    string LegalText,

    /// <summary>
    /// The faint picture drawn once across the whole sheet, behind both faces.
    /// Null means the sheet is left plain. Shared by every credential of the
    /// competition, which is why it lives here and not on each print.
    /// </summary>
    string? BackgroundKey = null);

/// <summary>One credential, ready to be drawn.</summary>
/// <remarks>
/// Everything here is already a fact — the category resolved, the grants
/// merged from the package and its exceptions, the serial drawn, the QR's
/// address built. Nothing in <c>CredentialRenderer</c> looks anything up: it
/// only lays these fields onto the fixed structure the card is printed to.
/// </remarks>
public sealed record CredentialPrint(
    CredentialSubject Subject,
    CredentialContext Context,
    string CategoryCode,
    string CategoryName,
    string CategoryColorHex,

    /// <summary>
    /// The identifier printed in the data block — a delegation code and a
    /// number, not the QR's own serial. See <c>Serial</c> for why the two are
    /// different things printed for different reasons.
    /// </summary>
    string VisibleId,

    string VerifyUrl,
    IReadOnlyList<CredentialGrant> Grants,
    DateTimeOffset IssuedAt)
{
    public IEnumerable<CredentialGrant> Zones => Grants.Where(grant => grant.Kind == AccreditationItemKind.Zone);

    private IEnumerable<CredentialGrant> FirstRow => Grants.Where(grant =>
        grant.Kind is AccreditationItemKind.Discipline or AccreditationItemKind.Venue);

    private IEnumerable<CredentialGrant> SecondRow =>
        Grants.Where(grant => grant.Kind == AccreditationItemKind.Service);

    /// <summary>The front's two rows of boxes, in printing order.</summary>
    public IReadOnlyList<IReadOnlyList<CredentialGrant>> Rows => [[.. FirstRow], [.. SecondRow]];

    /// <summary>
    /// The colour the footer band is filled with.
    /// </summary>
    /// <remarks>
    /// The first coloured zone this person holds, in the order the catalogue
    /// gives its zones — matching how an Olympic-system card paints "Blue" or
    /// "Red" zone access as a colour strip rather than as a word (see
    /// <c>accreditation_items.color_hex</c>). A person can only ever carry one
    /// such zone in practice; the category's own colour is the fallback for
    /// the rare case none of their zones carries one, so the band is never
    /// left blank.
    /// </remarks>
    public string BandColorHex => Zones.Select(zone => zone.ColorHex).FirstOrDefault(hex => hex is not null)
        ?? CategoryColorHex;
}
