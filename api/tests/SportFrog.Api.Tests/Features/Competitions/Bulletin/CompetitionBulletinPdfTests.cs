using AwesomeAssertions;
using SportFrog.Api.Features.Competitions.Bulletin;

namespace SportFrog.Api.Tests.Features.Competitions.Bulletin;

/// <summary>
/// QuestPDF's own layout checks only run inside <c>GeneratePdf()</c> — a
/// composition that compiles can still throw at render time if a table or a
/// column is asked to do something the layout engine refuses. This is the
/// only thing that would catch that before an organizer does.
/// </summary>
public sealed class CompetitionBulletinPdfTests
{
    private static readonly BulletinCategory Category = new(
        "Sub-15 Masculino", "M", "Nacidos entre el 01/01/2011 y el 31/12/2012", "Hasta 45 kg", 20,
        "2 tiempos de 25 minutos",
        ["Victoria: 3 puntos", "Empate: 1 punto", "Derrota: 0 puntos"],
        ["Diferencia de goles", "Enfrentamiento directo"],
        IsJudged: false);

    private static readonly BulletinCategory JudgedCategory = new(
        "Poomsae Individual", null, null, null, null,
        "1 actuación",
        ["Victoria", "Derrota"],
        [],
        IsJudged: true);

    // Un PNG de 1x1 valido, minimo -- alcanza para que QuestPDF lo decodifique
    // de verdad al generar, sin depender de una imagen real en el repositorio.
    private static readonly byte[] TinyLogo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static BulletinData Data(
        IReadOnlyList<BulletinCategory>? categories = null, string? longText = null, byte[]? logoBytes = null,
        string? accentColor = null) => new(
        "Federación de Prueba", "Copa Apertura", "Fútbol", "2026", "Todos contra todos",
        new DateOnly(2026, 5, 1), new DateOnly(2026, 8, 30),
        categories ?? [Category],
        Introduction: longText ?? "Bienvenidos a la Copa Apertura 2026.\n\nSegundo párrafo de la presentación.",
        Sanctions: longText ?? "Una amarilla suspende un partido. Dos rojas, dos partidos.",
        GeneralProvisions: longText ?? "Los partidos se juegan según el fixture publicado.",
        ContactInfo: longText ?? "consultas@federacion.example",
        LogoBytes: logoBytes,
        AccentColor: accentColor);

    [Fact]
    public void Render_TypicalCompetition_ProducesAPdf()
    {
        var bytes = CompetitionBulletinPdf.Render(Data());

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_JudgedCategory_ProducesAPdf()
    {
        var bytes = CompetitionBulletinPdf.Render(Data([JudgedCategory]));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_NoOrganizerProse_ProducesAPdf()
    {
        // Toda la prosa del organizador es opcional -- una convocatoria
        // pedida antes de llenar esos campos no tiene por que fallar.
        var data = new BulletinData(
            "Federación de Prueba", "Copa Apertura", "Fútbol", "2026", "Todos contra todos",
            StartsOn: null, EndsOn: null, [Category],
            Introduction: null, Sanctions: null, GeneralProvisions: null, ContactInfo: null,
            LogoBytes: null, AccentColor: null);

        var bytes = CompetitionBulletinPdf.Render(data);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_WithLogo_ProducesAPdf()
    {
        var bytes = CompetitionBulletinPdf.Render(Data(logoBytes: TinyLogo));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_WithACompetitionColour_ProducesAPdf()
    {
        // No aserción sobre el color en si -- QuestPDF.GeneratePdf() es el
        // unico chequeo real, igual que el resto del archivo -- solo que un
        // hex invalido en Accent() rompa la generacion.
        var bytes = CompetitionBulletinPdf.Render(Data(accentColor: "#7B1FA2"));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ManyCategoriesAndLongProse_StillProducesAPdf()
    {
        // El motivo de que esto sea flujo y no un layout fijo: nada sabe de
        // antemano cuantas categorias o cuanto texto va a haber.
        var categories = Enumerable.Range(1, 25)
            .Select(i => Category with { Name = $"Categoría {i}" })
            .ToList();
        var longText = string.Join("\n\n", Enumerable.Repeat("Un párrafo largo de reglamento repetido varias veces.", 40));

        var bytes = CompetitionBulletinPdf.Render(Data(categories, longText));

        bytes.Should().NotBeEmpty();
    }
}
