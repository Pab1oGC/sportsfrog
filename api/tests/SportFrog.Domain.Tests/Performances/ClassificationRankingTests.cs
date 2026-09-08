using AwesomeAssertions;
using SportFrog.Domain.Performances;

namespace SportFrog.Domain.Tests.Performances;

/// <summary>
/// Turns a classification stage's performances into a ranking. Expected
/// positions below are worked out by hand from "highest judged score wins,
/// ties share a place, a team that has not performed yet has no place at
/// all" — the same tie-sharing rule LeaderboardTests already establishes for
/// SportFrog.Domain.Statistics.Leaderboard, applied here to a different
/// shape of input.
/// </summary>
public sealed class ClassificationRankingTests
{
    private static PerformanceEntry Entry(string team, int? score, PerformanceStatus status) =>
        new(Guid.NewGuid(), Guid.NewGuid(), team, status, score);

    private static PerformanceEntry Scored(string team, int score) =>
        Entry(team, score, PerformanceStatus.Scored);

    private static PerformanceEntry Pending(string team) =>
        Entry(team, null, PerformanceStatus.Pending);

    [Fact]
    public void Rank_OrdersByScoreDescending()
    {
        var entries = new[] { Scored("A", 700), Scored("B", 900), Scored("C", 800) };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Select(item => item.Entry.TeamName).Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public void Rank_TiedScores_SharePlaceAndNextDistinctScoreSkipsAhead()
    {
        // Two teams tied for first, one alone in third: 1, 1, 3 — not 1, 1, 2.
        var entries = new[] { Scored("A", 900), Scored("B", 900), Scored("C", 800) };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Should().ContainSingle(item => item.Entry.TeamName == "A" && item.Position == 1);
        ranked.Should().ContainSingle(item => item.Entry.TeamName == "B" && item.Position == 1);
        ranked.Should().ContainSingle(item => item.Entry.TeamName == "C" && item.Position == 3);
    }

    [Fact]
    public void Rank_TiedScore_BreaksByTeamNameForAStableOrder()
    {
        var entries = new[] { Scored("Zeta", 900), Scored("Alfa", 900) };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Select(item => item.Entry.TeamName).Should().ContainInOrder("Alfa", "Zeta");
    }

    [Fact]
    public void Rank_TeamNotYetPerformed_HasNoPosition()
    {
        var entries = new[] { Scored("A", 900), Pending("B") };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Single(item => item.Entry.TeamName == "A").Position.Should().Be(1);
        ranked.Single(item => item.Entry.TeamName == "B").Position.Should().BeNull();
    }

    [Fact]
    public void Rank_TeamsNotYetPerformed_AreListedAfterEveryScoredTeam()
    {
        var entries = new[] { Pending("B"), Scored("A", 500) };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Select(item => item.Entry.TeamName).Should().ContainInOrder("A", "B");
    }

    [Fact]
    public void Rank_MultiplePendingTeams_AreOrderedByNameForAStableListing()
    {
        var entries = new[] { Pending("Zeta"), Pending("Alfa") };

        var ranked = ClassificationRanking.Rank(entries);

        ranked.Select(item => item.Entry.TeamName).Should().ContainInOrder("Alfa", "Zeta");
        ranked.Should().OnlyContain(item => item.Position == null);
    }

    [Fact]
    public void Rank_EveryEntryIsReturned_NoneDroppedTheWayALeaderboardTopCutoffWould()
    {
        // Unlike Leaderboard.Rank, a classification listing has no "top N" —
        // an organizer needs to see every entrant, not just the leaders.
        var entries = Enumerable.Range(0, 20).Select(i => Scored($"Team{i}", i)).ToArray();

        ClassificationRanking.Rank(entries).Should().HaveCount(20);
    }

    [Fact]
    public void Rank_NoEntries_ProducesAnEmptyRanking()
    {
        ClassificationRanking.Rank([]).Should().BeEmpty();
    }
}
