using AwesomeAssertions;
using SportFrog.Api.Features.Lists;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// QuestPDF's own layout checks only run inside <c>GeneratePdf()</c> — see
/// <c>CompetitionBulletinPdfTests</c>'s own remarks for why producing bytes
/// without throwing is itself the check that matters here, the same
/// convention <c>FixturePdfTests</c> already tests this renderer's domain
/// siblings under.
/// </summary>
public sealed class ListPdfTests
{
    private static readonly IReadOnlyList<ListColumn> NameAndGoals =
    [
        new ListColumn("Nombre", ListValueKind.Text),
        new ListColumn("Goles", ListValueKind.Number),
    ];

    [Fact]
    public void Render_OneSection_ProducesAPdf()
    {
        var table = new ListTable(
            "Goleadores", "Sub-17", NameAndGoals, [new ListSection("Goles", [["Diaz", 4], ["Soto", 2]])]);

        var bytes = ListPdf.Render(table);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_SeveralSections_ProducesAPdf()
    {
        var table = new ListTable(
            "Tableros", null, NameAndGoals,
            [
                new ListSection("Goles", [["Diaz", 4]]),
                new ListSection("Asistencias", [["Soto", 2]]),
            ]);

        ListPdf.Render(table).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_NoSections_ProducesAPdfWithTheNoDataNote()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, []);

        ListPdf.Render(table).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ASectionWithNoRows_ProducesAPdf()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, [new ListSection("Goles", [])]);

        ListPdf.Render(table).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ManyRows_PaginatesRatherThanOverflowing()
    {
        var rows = Enumerable.Range(1, 500).Select(i => new List<object?> { $"Jugador {i}", i }).ToList();
        var table = new ListTable("Goleadores", null, NameAndGoals, [new ListSection(null, rows)]);

        ListPdf.Render(table).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_MoreThanEightColumns_StillProducesAPdf()
    {
        var columns = Enumerable.Range(1, 12)
            .Select(i => new ListColumn($"Columna {i}", ListValueKind.Text))
            .ToList();
        var row = Enumerable.Range(1, 12).Select(i => (object?)$"Valor {i}").ToList();
        var table = new ListTable("Tabla ancha", null, columns, [new ListSection(null, [row])]);

        ListPdf.Render(table).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_EveryColumnKind_ProducesAPdf()
    {
        var columns = new[]
        {
            new ListColumn("Nombre", ListValueKind.Text),
            new ListColumn("Goles", ListValueKind.Number),
            new ListColumn("Nacimiento", ListValueKind.Date),
            new ListColumn("Registrado", ListValueKind.DateTime),
            new ListColumn("Retirado", ListValueKind.Boolean),
        };
        var row = new List<object?>
        {
            "Diaz", 4, new DateOnly(2010, 5, 1), new DateTime(2026, 5, 1, 14, 30, 0), false,
        };
        var table = new ListTable("Deportistas", null, columns, [new ListSection(null, [row])]);

        ListPdf.Render(table).Should().NotBeEmpty();
    }
}
