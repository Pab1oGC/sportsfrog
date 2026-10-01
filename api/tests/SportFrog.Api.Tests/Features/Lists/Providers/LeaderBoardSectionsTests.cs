using AwesomeAssertions;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Features.Statistics;
using SportFrog.Domain.Statistics;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>
/// <see cref="LeaderBoardSections.ToSection"/> is pure — given a
/// <see cref="RankedBoard"/>, what rows come out — and pinned here without a
/// database. <see cref="LeadersList"/> and <see cref="CardsList"/> each cover
/// separately which boards they ask for and how they filter them.
/// </summary>
public sealed class LeaderBoardSectionsTests
{
    private static Tally Player(string first, string last, short? jersey, string team, int total) =>
        new(null, Guid.NewGuid(), Guid.NewGuid(), first, last, jersey, Guid.NewGuid(), team, total);

    [Fact]
    public void ToSection_UsesTheBoardsMetricLabelAsTheSectionLabel()
    {
        var board = new RankedBoard(
            Guid.NewGuid(), "goal", "Gol", true, [(1, Player("Juan", "Diaz", 9, "Equipo A", 4))]);

        LeaderBoardSections.ToSection(board).Label.Should().Be("Gol");
    }

    [Fact]
    public void ToSection_ARow_CarriesPositionNameJerseyTeamAndTotal()
    {
        var board = new RankedBoard(
            Guid.NewGuid(), "goal", "Gol", true, [(1, Player("Juan", "Diaz", 9, "Equipo A", 4))]);

        var row = LeaderBoardSections.ToSection(board).Rows.Single();

        row.Should().Equal(1, "Diaz, Juan", "9", "Equipo A", 4);
    }

    [Fact]
    public void ToSection_NoJerseyNumber_ShowsADashRatherThanBlank()
    {
        var board = new RankedBoard(
            Guid.NewGuid(), "assist", "Asistencia", false, [(1, Player("Juan", "Diaz", null, "Equipo A", 2))]);

        LeaderBoardSections.ToSection(board).Rows.Single()[2].Should().Be("-");
    }

    [Fact]
    public void ToSection_SeveralLeaders_PreservesTheirGivenOrder()
    {
        var board = new RankedBoard(
            Guid.NewGuid(), "goal", "Gol", true,
            [
                (1, Player("Juan", "Diaz", 9, "Equipo A", 4)),
                (2, Player("Ana", "Soto", 10, "Equipo B", 2)),
            ]);

        var rows = LeaderBoardSections.ToSection(board).Rows;

        rows[0][1].Should().Be("Diaz, Juan");
        rows[1][1].Should().Be("Soto, Ana");
    }

    [Fact]
    public void ToSection_NoLeaders_ProducesAnEmptySection()
    {
        var board = new RankedBoard(Guid.NewGuid(), "red_card", "Tarjeta roja", false, []);

        LeaderBoardSections.ToSection(board).Rows.Should().BeEmpty();
    }
}
