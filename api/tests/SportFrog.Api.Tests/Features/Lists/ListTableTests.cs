using AwesomeAssertions;
using SportFrog.Api.Features.Lists;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// A renderer trusts that every row carries exactly one value per declared
/// column — it is what lets a PDF line up a header over the right cell and
/// an Excel sheet type a column without inspecting each value. These pin
/// that <see cref="ListTable"/> checks it once, at construction, rather than
/// leaving a misaligned row for a renderer to discover later as a cell under
/// the wrong header.
/// </summary>
public sealed class ListTableTests
{
    private static readonly IReadOnlyList<ListColumn> TwoColumns =
    [
        new ListColumn("Nombre", ListValueKind.Text),
        new ListColumn("Goles", ListValueKind.Number),
    ];

    [Fact]
    public void Construction_EveryRowMatchesColumnCount_Succeeds()
    {
        var sections = new[]
        {
            new ListSection("Goles", [["Diaz", 4], ["Soto", 2]]),
        };

        var table = new ListTable("Goleadores", "Sub-17", TwoColumns, sections);

        table.Sections.Should().BeEquivalentTo(sections);
    }

    [Fact]
    public void Construction_ARowWithFewerValuesThanColumns_Throws()
    {
        var sections = new[]
        {
            new ListSection("Goles", [["Diaz"]]),
        };

        var building = () => new ListTable("Goleadores", null, TwoColumns, sections);

        building.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construction_ARowWithMoreValuesThanColumns_Throws()
    {
        var sections = new[]
        {
            new ListSection("Goles", [["Diaz", 4, "extra"]]),
        };

        var building = () => new ListTable("Goleadores", null, TwoColumns, sections);

        building.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construction_NoSections_Succeeds()
    {
        var table = new ListTable("Goleadores", null, TwoColumns, []);

        table.Sections.Should().BeEmpty();
    }

    [Fact]
    public void Construction_ASectionWithNoRows_Succeeds()
    {
        var sections = new[] { new ListSection("Goles", []) };

        var table = new ListTable("Goleadores", null, TwoColumns, sections);

        table.Sections.Should().ContainSingle().Which.Rows.Should().BeEmpty();
    }
}
