using AwesomeAssertions;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Infrastructure.Storage;

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

    // A genuine 1x1 transparent PNG — small enough to inline, real enough
    // that a decoder that would reject bad image bytes still accepts it.
    private static readonly byte[] OnePixelPng =
    [
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21,
        196, 137, 0, 0, 0, 10, 73, 68, 65, 84, 120, 156, 99, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73,
        69, 78, 68, 174, 66, 96, 130,
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

    /// <summary>
    /// A short column (a position, a tally, a yes/no) never needs the room a
    /// team's own name does — pinned directly, without rendering a PDF, the
    /// bug an equal <c>RelativeColumn()</c> on every column actually shipped
    /// with: a "Pos." column exactly as wide as "Equipo".
    /// </summary>
    [Fact]
    public void ColumnWeight_TextGetsMoreRoomThanAnyShortColumn()
    {
        ListPdf.ColumnWeight(ListValueKind.Text).Should().BeGreaterThan(ListPdf.ColumnWeight(ListValueKind.Number));
        ListPdf.ColumnWeight(ListValueKind.Text).Should().BeGreaterThan(ListPdf.ColumnWeight(ListValueKind.Boolean));
        ListPdf.ColumnWeight(ListValueKind.Text).Should().BeGreaterThan(ListPdf.ColumnWeight(ListValueKind.Date));
        ListPdf.ColumnWeight(ListValueKind.Text).Should().BeGreaterThan(ListPdf.ColumnWeight(ListValueKind.DateTime));
    }

    [Fact]
    public void ColumnWeight_NumberAndBoolean_AreTheNarrowestKinds()
    {
        ListPdf.ColumnWeight(ListValueKind.Number).Should().BeLessThan(ListPdf.ColumnWeight(ListValueKind.Date));
        ListPdf.ColumnWeight(ListValueKind.Boolean).Should().BeLessThan(ListPdf.ColumnWeight(ListValueKind.Date));
    }

    [Fact]
    public void AlignmentFor_ANumberColumnNotFirst_IsRight()
    {
        ListPdf.AlignmentFor(ListValueKind.Number, isFirstColumn: false).Should().Be(ListPdf.CellAlignment.Right);
    }

    /// <summary>
    /// The one exception: a first column that is a <see cref="ListValueKind.Number"/>
    /// is always a row's position, never a quantity worth lining up by
    /// magnitude — right-aligned, it sits pinned against the inner border
    /// with nothing but empty space ahead of it on every single row, which
    /// is the "primera columna... ocupa mucho espacio hacia su izquierda"
    /// this pins against regressing back to.
    /// </summary>
    [Fact]
    public void AlignmentFor_ANumberColumnThatIsFirst_IsCenterRatherThanRight()
    {
        ListPdf.AlignmentFor(ListValueKind.Number, isFirstColumn: true).Should().Be(ListPdf.CellAlignment.Center);
    }

    [Fact]
    public void AlignmentFor_ABooleanColumn_IsAlwaysCenter_FirstOrNot()
    {
        ListPdf.AlignmentFor(ListValueKind.Boolean, isFirstColumn: false).Should().Be(ListPdf.CellAlignment.Center);
        ListPdf.AlignmentFor(ListValueKind.Boolean, isFirstColumn: true).Should().Be(ListPdf.CellAlignment.Center);
    }

    [Fact]
    public void AlignmentFor_EverythingElse_IsAlwaysLeft_FirstOrNot()
    {
        foreach (var kind in new[] { ListValueKind.Text, ListValueKind.Date, ListValueKind.DateTime })
        {
            ListPdf.AlignmentFor(kind, isFirstColumn: false).Should().Be(ListPdf.CellAlignment.Left);
            ListPdf.AlignmentFor(kind, isFirstColumn: true).Should().Be(ListPdf.CellAlignment.Left);
        }
    }

    [Fact]
    public void Accent_NoColourSet_FallsBackToTheSameBlueEveryOtherDocumentUses()
    {
        ListPdf.Accent(null).ToString().Should().Be("#1976D2");
        ListPdf.Accent("").ToString().Should().Be("#1976D2");
        ListPdf.Accent("   ").ToString().Should().Be("#1976D2");
    }

    [Fact]
    public void Accent_ACompetitionsOwnColour_IsUsedAsIs()
    {
        ListPdf.Accent("#ff0000").ToString().Should().Be("#FF0000");
    }

    [Fact]
    public void Render_WithALogo_StillProducesAPdf()
    {
        var table = new ListTable(
            "Goleadores", "Sub-17", NameAndGoals, [new ListSection("Goles", [["Diaz", 4]])]);
        var branding = new CompetitionBranding(OnePixelPng, "#ff0000");

        var bytes = ListPdf.Render(table, branding);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_BrandingWithNoLogoOrColour_RendersTheSameAsNoBrandingAtAll()
    {
        var table = new ListTable("Goleadores", "Sub-17", NameAndGoals, [new ListSection("Goles", [["Diaz", 4]])]);

        ListPdf.Render(table, CompetitionBranding.None).Should().NotBeEmpty();
    }
}
