using AwesomeAssertions;
using ClosedXML.Excel;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// <see cref="ListXlsx"/> is read back with ClosedXML itself rather than
/// inspected as raw bytes — the only way to pin that a numeric column really
/// lands as a number Excel can sum, not text that only looks like one.
/// </summary>
public sealed class ListXlsxTests
{
    private static readonly IReadOnlyList<ListColumn> NameAndGoals =
    [
        new ListColumn("Nombre", ListValueKind.Text),
        new ListColumn("Goles", ListValueKind.Number),
    ];

    // A genuine 1x1 transparent PNG — small enough to inline, real enough
    // that ClosedXML's own decoder still accepts it as a picture.
    private static readonly byte[] OnePixelPng =
    [
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21,
        196, 137, 0, 0, 0, 10, 73, 68, 65, 84, 120, 156, 99, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73,
        69, 78, 68, 174, 66, 96, 130,
    ];

    private static XLWorkbook Reopen(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Render_OneSection_WritesOneWorksheet()
    {
        var table = new ListTable(
            "Goleadores", "Sub-17", NameAndGoals,
            [new ListSection("Goles", [["Diaz", 4], ["Soto", 2]])]);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.Should().ContainSingle();
    }

    [Fact]
    public void Render_SeveralSections_WritesOneWorksheetPerSection()
    {
        var table = new ListTable(
            "Tableros", null, NameAndGoals,
            [
                new ListSection("Goles", [["Diaz", 4]]),
                new ListSection("Asistencias", [["Soto", 2]]),
            ]);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.Select(sheet => sheet.Name).Should().BeEquivalentTo(["Goles", "Asistencias"]);
    }

    [Fact]
    public void Render_ANumericColumn_LandsAsANumberRatherThanText()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, [new ListSection(null, [["Diaz", 4]])]);

        using var workbook = Reopen(ListXlsx.Render(table));
        var cell = workbook.Worksheets.First().Cell(5, 2);

        cell.DataType.Should().Be(XLDataType.Number);
        cell.GetDouble().Should().Be(4);
    }

    [Fact]
    public void Render_ATextColumn_LandsAsTextEvenWhenItLooksNumeric()
    {
        var columns = new[] { new ListColumn("Documento", ListValueKind.Text) };
        var table = new ListTable("Deportistas", null, columns, [new ListSection(null, [["00071"]])]);

        using var workbook = Reopen(ListXlsx.Render(table));
        var cell = workbook.Worksheets.First().Cell(5, 1);

        cell.DataType.Should().Be(XLDataType.Text);
        cell.GetString().Should().Be("00071");
    }

    [Fact]
    public void Render_ADateColumn_LandsAsADate()
    {
        var columns = new[] { new ListColumn("Nacimiento", ListValueKind.Date) };
        var table = new ListTable(
            "Deportistas", null, columns, [new ListSection(null, [[new DateOnly(2026, 5, 1)]])]);

        using var workbook = Reopen(ListXlsx.Render(table));
        var cell = workbook.Worksheets.First().Cell(5, 1);

        cell.DataType.Should().Be(XLDataType.DateTime);
        cell.GetDateTime().Should().Be(new DateTime(2026, 5, 1));
    }

    [Fact]
    public void Render_HeaderRow_CarriesEachColumnsHeader()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, [new ListSection(null, [["Diaz", 4]])]);

        using var workbook = Reopen(ListXlsx.Render(table));
        var sheet = workbook.Worksheets.First();

        sheet.Cell(4, 1).GetString().Should().Be("Nombre");
        sheet.Cell(4, 2).GetString().Should().Be("Goles");
    }

    [Fact]
    public void Render_NoSections_StillProducesOneOpenableSheetWithANoDataNote()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, []);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.Should().ContainSingle();
        workbook.Worksheets.First().CellsUsed(cell => cell.GetString() == ListMessages.NoData).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ASectionWithNoRows_ShowsTheNoDataNoteRatherThanAnEmptyTable()
    {
        var table = new ListTable("Goleadores", null, NameAndGoals, [new ListSection("Goles", [])]);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.First().CellsUsed(cell => cell.GetString() == ListMessages.NoData).Should().NotBeEmpty();
    }

    [Fact]
    public void Render_TwoSectionsWithTheSameLabel_GetDistinctSheetNames()
    {
        var table = new ListTable(
            "Tableros", null, NameAndGoals,
            [
                new ListSection("Grupo A", [["Diaz", 4]]),
                new ListSection("Grupo A", [["Soto", 2]]),
            ]);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.Select(sheet => sheet.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Render_ASectionLabelWithExcelReservedCharacters_SanitizesTheSheetName()
    {
        var table = new ListTable(
            "Partidos", null, NameAndGoals, [new ListSection("Grupo A / B", [["Diaz", 4]])]);

        using var workbook = Reopen(ListXlsx.Render(table));

        workbook.Worksheets.First().Name.Should().NotContain("/");
    }

    [Fact]
    public void AccentColor_NoColourSet_FallsBackToTheSameBlueEveryOtherDocumentUses()
    {
        ListXlsx.AccentColor(null).Should().Be(XLColor.FromHtml("#1976D2"));
        ListXlsx.AccentColor("").Should().Be(XLColor.FromHtml("#1976D2"));
        ListXlsx.AccentColor("   ").Should().Be(XLColor.FromHtml("#1976D2"));
    }

    [Fact]
    public void AccentColor_ACompetitionsOwnColour_IsUsedAsIs()
    {
        ListXlsx.AccentColor("#ff0000").Should().Be(XLColor.FromHtml("#ff0000"));
    }

    [Fact]
    public void Render_WithALogo_ShiftsTheTitleOverToMakeRoomForIt()
    {
        var table = new ListTable(
            "Goleadores", "Sub-17", NameAndGoals, [new ListSection("Goles", [["Diaz", 4]])]);
        var branding = new CompetitionBranding(OnePixelPng, "#ff0000");

        using var workbook = Reopen(ListXlsx.Render(table, branding));
        var sheet = workbook.Worksheets.First();

        sheet.Cell(1, 2).GetString().Should().Be("Goleadores");
        sheet.Cell(2, 2).GetString().Should().Be("Sub-17");
        sheet.Pictures.Should().ContainSingle();
    }

    [Fact]
    public void Render_BrandingWithNoLogoOrColour_RendersTheSameAsNoBrandingAtAll()
    {
        var table = new ListTable("Goleadores", "Sub-17", NameAndGoals, [new ListSection("Goles", [["Diaz", 4]])]);

        using var workbook = Reopen(ListXlsx.Render(table, CompetitionBranding.None));
        var sheet = workbook.Worksheets.First();

        sheet.Cell(1, 1).GetString().Should().Be("Goleadores");
        sheet.Pictures.Should().BeEmpty();
    }
}
