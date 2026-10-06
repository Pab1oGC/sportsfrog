using AwesomeAssertions;
using SportFrog.Api.Features.Documents;
using SportFrog.Domain.Accreditation;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// QuestPDF's own layout checks only run inside <c>GeneratePdf()</c> — see
/// <c>CompetitionBulletinPdfTests</c>'s own remarks for why this is the only
/// thing that would catch a composition mistake before an organizer does.
/// Every fixed block of <see cref="CredentialLayout"/> — the photo, the
/// category box, both rows, the QR, the footer band on both faces, the
/// two-column legal notice, the identity backup, the glossary — is drawn by
/// at least one test below, so a block that would throw at render time is
/// caught here rather than the first time a batch tries to print it.
/// </summary>
public sealed class CredentialRendererTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static CredentialContext Context(string legalText = "Texto legal de la organización.", string? accentColorHex = null) => new(
        OrganizationName: "Liga Departamental de Fútbol",
        CompetitionName: "Copa Apertura",
        Season: "2026",
        CompetitionLogoKey: null,
        AccentColorHex: accentColorHex,
        LegalText: legalText);

    private static CredentialSubject Subject(string fullName = "Axel Salvador Quijada Miranda") => new(
        fullName, PhotoKey: null, ClubName: "Club Atlético Independiente", ClubLogoKey: null);

    private static CredentialGrant Grant(
        AccreditationItemKind kind, string code, string? colorHex = null, Guid? itemId = null) =>
        new(itemId ?? Guid.NewGuid(), kind, code, $"Nombre de {code}", colorHex, IconKey: null);

    private static IReadOnlyList<CredentialGrant> StandardGrants() =>
    [
        Grant(AccreditationItemKind.Discipline, "FUT"),
        Grant(AccreditationItemKind.Venue, "VIL"),
        Grant(AccreditationItemKind.Venue, "MPC"),
        Grant(AccreditationItemKind.Service, "TA"),
        Grant(AccreditationItemKind.Service, "COM"),
        Grant(AccreditationItemKind.Zone, "AZUL", colorHex: "#1F3864"),
        Grant(AccreditationItemKind.Zone, "2"),
        Grant(AccreditationItemKind.Zone, "R"),
    ];

    private static CredentialPrint Print(
        IReadOnlyList<CredentialGrant>? grants = null, CredentialContext? context = null, CredentialSubject? subject = null) =>
        new(
            subject ?? Subject(),
            context ?? Context(),
            CategoryCode: "Aa",
            CategoryName: "Deportista",
            CategoryColorHex: "#1F3864",
            VisibleId: "IND-0042",
            VerifyUrl: "https://sportfrog.app/liga/IND-0042",
            Grants: grants ?? StandardGrants(),
            IssuedAt: DateTimeOffset.UtcNow);

    private static void AssertPdf(byte[] bytes)
    {
        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_EveryAssetPresent_ProducesAPdf()
    {
        var assets = new CredentialAssets(
            Photo: OnePixelPng, ClubLogo: OnePixelPng, CompetitionLogo: OnePixelPng, Qr: OnePixelPng);

        AssertPdf(CredentialRenderer.Render(Print(), assets));
    }

    [Fact]
    public void Render_NoAssetsAtAll_DrawsPlaceholdersAndStillProducesAPdf()
    {
        var assets = new CredentialAssets(Photo: null, ClubLogo: null, CompetitionLogo: null, Qr: null);

        AssertPdf(CredentialRenderer.Render(Print(), assets));
    }

    [Fact]
    public void Render_CatalogueHasNoServices_SecondRowIsEmptyAndStillProducesAPdf()
    {
        IReadOnlyList<CredentialGrant> grants =
        [
            Grant(AccreditationItemKind.Discipline, "FUT"),
            Grant(AccreditationItemKind.Zone, "AZUL", colorHex: "#1F3864"),
        ];

        AssertPdf(CredentialRenderer.Render(
            Print(grants), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_NoZoneCarriesAColour_FootballBandFallsBackToTheCategoryColourAndStillProducesAPdf()
    {
        IReadOnlyList<CredentialGrant> grants =
        [
            Grant(AccreditationItemKind.Discipline, "FUT"),
            Grant(AccreditationItemKind.Zone, "2"),
            Grant(AccreditationItemKind.Zone, "R"),
        ];

        AssertPdf(CredentialRenderer.Render(
            Print(grants), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_PersonHoldsNoZoneAtAll_FooterBandIsEmptyAndStillProducesAPdf()
    {
        IReadOnlyList<CredentialGrant> grants = [Grant(AccreditationItemKind.Discipline, "FUT")];

        AssertPdf(CredentialRenderer.Render(
            Print(grants), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_ServiceHasAnIcon_DrawsTheIconInsteadOfTheCodeAndStillProducesAPdf()
    {
        var itemId = Guid.NewGuid();
        IReadOnlyList<CredentialGrant> grants =
        [
            Grant(AccreditationItemKind.Discipline, "FUT"),
            new(itemId, AccreditationItemKind.Service, "COM", "Comedor", null, IconKey: "orgs/x/icons/comedor.png"),
        ];

        var assets = new CredentialAssets(
            null, null, null, null, Icons: new Dictionary<Guid, byte[]> { [itemId] = OnePixelPng });

        AssertPdf(CredentialRenderer.Render(Print(grants), assets));
    }

    [Fact]
    public void Render_LegalTextIsEmpty_SplitsIntoTwoEmptyColumnsAndStillProducesAPdf()
    {
        AssertPdf(CredentialRenderer.Render(
            Print(context: Context(legalText: string.Empty)), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_LegalTextIsOneLongWordWithNoSpace_CannotSplitButStillProducesAPdf()
    {
        var oneWord = new string('x', 400);

        AssertPdf(CredentialRenderer.Render(
            Print(context: Context(legalText: oneWord)), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_ManyGrantsOfTheSameKind_RowStillFitsAndProducesAPdf()
    {
        IReadOnlyList<CredentialGrant> grants =
        [
            Grant(AccreditationItemKind.Discipline, "FUT"),
            Grant(AccreditationItemKind.Venue, "VIL"),
            Grant(AccreditationItemKind.Venue, "MPC"),
            Grant(AccreditationItemKind.Venue, "EST"),
            Grant(AccreditationItemKind.Venue, "PCD"),
            Grant(AccreditationItemKind.Venue, "VSA"),
        ];

        AssertPdf(CredentialRenderer.Render(
            Print(grants), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_FullNameIsBlank_DrawsThePlaceholderAndStillProducesAPdf()
    {
        AssertPdf(CredentialRenderer.Render(
            Print(subject: Subject(fullName: "   ")), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_CompetitionSetItsOwnAccentColour_ProducesAPdf()
    {
        AssertPdf(CredentialRenderer.Render(
            Print(context: Context(accentColorHex: "#C2185B")), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void Render_CompetitionNeverSetAnAccentColour_FallsBackToTheSystemDefaultAndStillProducesAPdf()
    {
        AssertPdf(CredentialRenderer.Render(
            Print(context: Context(accentColorHex: null)), new CredentialAssets(null, null, null, null)));
    }

    [Fact]
    public void RenderBatch_SeveralCredentials_ProducesOnePdf()
    {
        var assets = new CredentialAssets(null, null, null, null);

        List<(CredentialPrint, CredentialAssets)> credentials =
        [
            (Print(subject: Subject("Ana Rojas")), assets),
            (Print(subject: Subject("Beto Gómez")), assets),
            (Print(subject: Subject("Cami Díaz")), assets),
        ];

        AssertPdf(CredentialRenderer.RenderBatch(credentials));
    }

    [Fact]
    public void RenderBatch_OneCredential_ProducesOnePdf()
    {
        var assets = new CredentialAssets(null, null, null, null);

        AssertPdf(CredentialRenderer.RenderBatch([(Print(), assets)]));
    }
}
