using AwesomeAssertions;
using DocumentFormat.OpenXml.Packaging;
using SportFrog.Api.Features.Competitions.Bulletin;

namespace SportFrog.Api.Tests.Features.Competitions.Bulletin;

/// <summary>
/// Whether the composed OOXML actually opens as a document — a malformed
/// part or a missing relationship compiles fine and only shows up the moment
/// something tries to read the file back, which is exactly what an
/// organizer's copy of Word does immediately after downloading it.
/// </summary>
public sealed class CompetitionBulletinWordTests
{
    private static readonly BulletinCategory Category = new(
        "Sub-15 Masculino", "M", "Nacidos entre el 01/01/2011 y el 31/12/2012", "Hasta 45 kg", 20,
        "2 tiempos de 25 minutos",
        ["Victoria: 3 puntos", "Empate: 1 punto", "Derrota: 0 puntos"],
        ["Diferencia de goles", "Enfrentamiento directo"],
        IsJudged: false);

    // Un PNG de 1x1 valido, minimo -- alcanza para que el logo se incruste de
    // verdad al generar, sin depender de una imagen real en el repositorio.
    private static readonly byte[] TinyLogo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static BulletinData Data(IReadOnlyList<BulletinCategory>? categories = null, byte[]? logoBytes = null) => new(
        "Federación de Prueba", "Copa Apertura", "Fútbol", "2026", "Todos contra todos",
        new DateOnly(2026, 5, 1), new DateOnly(2026, 8, 30),
        categories ?? [Category],
        Introduction: "Bienvenidos a la Copa Apertura 2026.\n\nSegundo párrafo.",
        Sanctions: "Una amarilla suspende un partido.",
        GeneralProvisions: "Los partidos se juegan según el fixture publicado.",
        ContactInfo: "consultas@federacion.example",
        LogoBytes: logoBytes,
        AccentColor: null);

    [Fact]
    public void Render_TypicalCompetition_OpensAsAValidDocument()
    {
        var bytes = CompetitionBulletinWord.Render(Data());

        bytes.Should().NotBeEmpty();

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        var body = document.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;

        text.Should().Contain("Copa Apertura");
        text.Should().Contain("Convocatoria");
        text.Should().Contain("Sub-15 Masculino");
        text.Should().Contain("Una amarilla suspende un partido.");
    }

    [Fact]
    public void Render_NoOrganizerProse_StillOpensAsAValidDocument()
    {
        var data = new BulletinData(
            "Federación de Prueba", "Copa Apertura", "Fútbol", "2026", "Todos contra todos",
            StartsOn: null, EndsOn: null, [Category],
            Introduction: null, Sanctions: null, GeneralProvisions: null, ContactInfo: null,
            LogoBytes: null, AccentColor: null);

        var bytes = CompetitionBulletinWord.Render(data);

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        document.MainDocumentPart!.Document!.Body!.InnerText.Should().Contain("Copa Apertura");
    }

    [Fact]
    public void Render_WithLogo_EmbedsItAndOpensAsAValidDocument()
    {
        var bytes = CompetitionBulletinWord.Render(Data(logoBytes: TinyLogo));

        bytes.Should().NotBeEmpty();

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        document.MainDocumentPart!.ImageParts.Should().ContainSingle();
        document.MainDocumentPart!.Document!.Body!.InnerText.Should().Contain("Copa Apertura");
    }

    [Fact]
    public void Render_JudgedCategory_MentionsClassification()
    {
        var judged = Category with { Name = "Poomsae Individual", IsJudged = true, Gender = null };

        var bytes = CompetitionBulletinWord.Render(Data([judged]));

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        document.MainDocumentPart!.Document!.Body!.InnerText.Should().Contain("clasificación previa");
    }
}
