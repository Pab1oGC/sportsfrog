using AwesomeAssertions;
using SportFrog.Domain.Statistics;

namespace SportFrog.Domain.Tests.Statistics;

/// <summary>
/// Turns raw totals into a ranking. Expected positions below are worked out
/// by hand from ordinary league-table tie rules — equal totals share a
/// place, and the next distinct total skips the places they used — not by
/// reading Rank and mirroring what it returns.
/// </summary>
public sealed class LeaderboardTests
{
    private static Tally Player(string first, string last, int total, Guid? metricId = null) =>
        new(metricId, Guid.NewGuid(), Guid.NewGuid(), first, last, null, Guid.NewGuid(), "Equipo", total);

    [Fact]
    public void Rank_OrdersByTotalDescending()
    {
        var tallies = new[] { Player("A", "Bajo", 5), Player("B", "Alto", 20), Player("C", "Medio", 10) };

        var ranked = Leaderboard.Rank(tallies, top: 10);

        ranked.Select(r => r.Player.FirstName).Should().ContainInOrder("B", "C", "A");
        ranked.Select(r => r.Position).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Rank_TiedTotals_ShareAPlaceAndTheNextOneSkipsAhead()
    {
        // Two tied for first (last names in alphabetical order, so the
        // tiebreak below doesn't reorder them), one alone in third — not
        // second.
        var tallies = new[]
        {
            Player("Ana", "Aguirre", 10), Player("Beto", "Blanco", 10), Player("Cira", "Cruz", 5),
        };

        var ranked = Leaderboard.Rank(tallies, top: 10);

        ranked.Select(r => r.Player.FirstName).Should().ContainInOrder("Ana", "Beto", "Cira");
        ranked.Select(r => r.Position).Should().ContainInOrder(1, 1, 3);
    }

    [Fact]
    public void Rank_TiedOnTotal_BreaksByLastNameThenFirstName()
    {
        // Same total, no criterion but the name to fall back on — and it has
        // to be the same order every time this is read.
        var tallies = new[] { Player("Zoe", "Vargas", 8), Player("Ana", "Rojas", 8) };

        var ranked = Leaderboard.Rank(tallies, top: 10);

        ranked.Select(r => r.Player.LastName).Should().ContainInOrder("Rojas", "Vargas");
    }

    [Fact]
    public void Rank_ATiedPlaceThatWouldOverflowTop_IsKeptWhole()
    {
        // top: 1, but two players are tied for first — dropping one of them
        // would publish "first: X" when a second player scored exactly the
        // same, which is simply wrong.
        var tallies = new[] { Player("A", "Uno", 10), Player("B", "Dos", 10), Player("C", "Tres", 5) };

        var ranked = Leaderboard.Rank(tallies, top: 1);

        ranked.Should().HaveCount(2);
        ranked.Should().OnlyContain(r => r.Position == 1);
    }

    [Fact]
    public void Rank_APlaceStartingPastTop_IsExcludedEntirely()
    {
        var tallies = new[] { Player("A", "Uno", 10), Player("B", "Dos", 5) };

        var ranked = Leaderboard.Rank(tallies, top: 1);

        ranked.Should().ContainSingle();
        ranked[0].Player.FirstName.Should().Be("A");
    }

    [Fact]
    public void Rank_NoTallies_IsAnEmptyBoardNotAnError()
    {
        Leaderboard.Rank([], top: 10).Should().BeEmpty();
    }

    [Fact]
    public void Rank_ACombinedTallyWithNoSingleMetric_RanksTheSameAsAnyOther()
    {
        // The points board built by combining several scoring metrics
        // carries no MetricId of its own — Rank never looks at it, only at
        // the total, so this has to work exactly like any other tally.
        var combined = Player("Sergio", "Torrez", 89, metricId: null);
        var single = Player("Hugo", "Rivera", 86, metricId: Guid.NewGuid());

        var ranked = Leaderboard.Rank([combined, single], top: 10);

        ranked.Select(r => r.Player.FirstName).Should().ContainInOrder("Sergio", "Hugo");
    }
}
