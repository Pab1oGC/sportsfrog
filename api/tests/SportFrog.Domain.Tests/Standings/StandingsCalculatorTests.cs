using AwesomeAssertions;
using SportFrog.Domain.Rules;
using SportFrog.Domain.Standings;

namespace SportFrog.Domain.Tests.Standings;

/// <summary>
/// A standings table built from results and a ruleset, with no database
/// underneath. Expected rows below are worked out by hand from ordinary
/// league-table arithmetic — points, goal difference, who played whom — not
/// by reading the tally/rank code and mirroring what it happens to produce.
/// </summary>
public sealed class StandingsCalculatorTests
{
    private static readonly Guid X = Guid.NewGuid();
    private static readonly Guid Y = Guid.NewGuid();
    private static readonly Guid Z = Guid.NewGuid();

    // Standard football scoring: three for a win, one for a draw.
    private static readonly RulesetConfiguration Football = new()
    {
        Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
        Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
        Tiebreakers = [Tiebreaker.ScoreDifference, Tiebreaker.ScoreFor],
    };

    private static StandingsRow Row(IReadOnlyList<StandingsGroup> groups, Guid teamId) =>
        groups.SelectMany(group => group.Rows).Single(row => row.TeamId == teamId);

    [Fact]
    public void Build_ThreeTeamRoundRobin_TalliesAndRanksByPoints()
    {
        var contenders = new[]
        {
            new Contender(X, "X", null),
            new Contender(Y, "Y", null),
            new Contender(Z, "Z", null),
        };

        // X beats Y 2-0, X beats Z 1-0, Y beats Z 3-1.
        var played = new[]
        {
            new PlayedMatch(X, Y, 2, 0),
            new PlayedMatch(X, Z, 1, 0),
            new PlayedMatch(Y, Z, 3, 1),
        };

        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Cumulative, Football);

        var x = Row(groups, X);
        x.Played.Should().Be(2);
        x.Won.Should().Be(2);
        x.Drawn.Should().Be(0);
        x.Lost.Should().Be(0);
        x.ScoreFor.Should().Be(3);
        x.ScoreAgainst.Should().Be(0);
        x.Points.Should().Be(6);

        var y = Row(groups, Y);
        y.Played.Should().Be(2);
        y.Won.Should().Be(1);
        y.Lost.Should().Be(1);
        y.ScoreFor.Should().Be(3);
        y.ScoreAgainst.Should().Be(3);
        y.Points.Should().Be(3);

        var z = Row(groups, Z);
        z.Played.Should().Be(2);
        z.Won.Should().Be(0);
        z.Lost.Should().Be(2);
        z.ScoreFor.Should().Be(1);
        z.ScoreAgainst.Should().Be(4);
        z.Points.Should().Be(0);

        // Unambiguous on points alone: 6, 3, 0.
        groups.Should().HaveCount(1);
        groups[0].Rows.Select(row => row.TeamId).Should().ContainInOrder(X, Y, Z);
    }

    [Fact]
    public void Build_TeamThatHasNotPlayedYet_StillAppearsWithAllZeros()
    {
        var contenders = new[] { new Contender(X, "X", null), new Contender(Y, "Y", null) };
        var played = new[] { new PlayedMatch(X, Y, 1, 0) };

        // Z is entered in the competition but its first fixture has not been
        // played. A team missing from the table would read as thrown out.
        var withZ = new[]
        {
            new Contender(X, "X", null), new Contender(Y, "Y", null), new Contender(Z, "Z", null),
        };

        var groups = StandingsCalculator.Build(withZ, played, ScoreMode.Cumulative, Football);

        var z = Row(groups, Z);
        z.Played.Should().Be(0);
        z.Won.Should().Be(0);
        z.Drawn.Should().Be(0);
        z.Lost.Should().Be(0);
        z.ScoreFor.Should().Be(0);
        z.ScoreAgainst.Should().Be(0);
        z.Points.Should().Be(0);
    }

    [Fact]
    public void Build_PointsComeFromTheRulesetAndNotAHardcodedScheme()
    {
        // Two for a win, one for a draw — not the 3/1/0 used elsewhere in
        // this file. If points were hardcoded anywhere in the calculator,
        // this would still come out 3/1/0 and the test would catch it.
        var hockeyStyle = new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = 3, Label = "período", Minutes = 20 },
            Points = new Dictionary<string, int> { ["win"] = 2, ["draw"] = 1, ["loss"] = 0 },
            Tiebreakers = [],
        };

        var contenders = new[] { new Contender(X, "X", null), new Contender(Y, "Y", null) };
        var played = new[] { new PlayedMatch(X, Y, 4, 4) }; // a draw

        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Cumulative, hockeyStyle);

        Row(groups, X).Points.Should().Be(1);
        Row(groups, Y).Points.Should().Be(1);
    }

    [Fact]
    public void Build_AnOutcomeTheRulesetDidNotPrice_IsWorthZeroRatherThanThrowing()
    {
        // A ruleset missing the "loss" key entirely. The rulebook module
        // would refuse to save this, but a table loaded from data written
        // another way must not blow up over a missing price.
        var incomplete = new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
            Points = new Dictionary<string, int> { ["win"] = 3 },
            Tiebreakers = [],
        };

        var contenders = new[] { new Contender(X, "X", null), new Contender(Y, "Y", null) };
        var played = new[] { new PlayedMatch(X, Y, 0, 1) }; // Y wins, X loses

        // Not wrapped in an explicit "should not throw" — an unhandled
        // exception here fails the test just as surely, and this is the
        // actual call being characterized.
        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Cumulative, incomplete);

        Row(groups, X).Points.Should().Be(0); // unpriced "loss"
        Row(groups, Y).Points.Should().Be(3); // priced "win"
    }

    [Fact]
    public void Build_GroupsAreTalliedSeparatelyAndDoNotMerge()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();

        var contenders = new[]
        {
            new Contender(a1, "A1", "A"),
            new Contender(a2, "A2", "A"),
            new Contender(b1, "B1", "B"),
            new Contender(b2, "B2", "B"),
        };

        var played = new[]
        {
            new PlayedMatch(a1, a2, 3, 0),
            new PlayedMatch(b1, b2, 0, 0),
        };

        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Cumulative, Football);

        groups.Should().HaveCount(2);

        var groupA = groups.Single(g => g.Label == "A");
        groupA.Rows.Should().HaveCount(2);
        groupA.Rows.Should().OnlyContain(row => row.TeamId == a1 || row.TeamId == a2);

        var groupB = groups.Single(g => g.Label == "B");
        groupB.Rows.Should().HaveCount(2);
        groupB.Rows.Should().OnlyContain(row => row.TeamId == b1 || row.TeamId == b2);

        // Neither group's tally leaked into the other's.
        groupA.Rows.Sum(row => row.Played).Should().Be(2);
        groupB.Rows.Sum(row => row.Played).Should().Be(2);
    }

    [Fact]
    public void Build_AMatchBetweenTeamsOfDifferentGroups_IsNotCountedInEitherTable()
    {
        // Should not happen in a well-drawn competition, but a bad draw
        // (or bad data loaded another way) must not half-count a cross-group
        // fixture into one side's tally, or crash trying.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var contenders = new[] { new Contender(a, "A", "Group A"), new Contender(b, "B", "Group B") };
        var played = new[] { new PlayedMatch(a, b, 2, 1) };

        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Cumulative, Football);

        Row(groups, a).Played.Should().Be(0);
        Row(groups, b).Played.Should().Be(0);
    }

    [Fact]
    public void Build_TeamsFullyTiedWithNoTiebreakersConfigured_FallBackToAlphabeticalOrder()
    {
        var contenders = new[]
        {
            new Contender(Z, "Zeta", null), new Contender(X, "Alpha", null), new Contender(Y, "Beta", null),
        };

        // Nothing played: every row is 0 points, fully tied, and there are
        // no tiebreak criteria to separate them.
        var noTiebreakers = Football with { Tiebreakers = [] };

        var groups = StandingsCalculator.Build(contenders, [], ScoreMode.Cumulative, noTiebreakers);

        groups[0].Rows.Select(row => row.TeamName).Should().ContainInOrder("Alpha", "Beta", "Zeta");
    }

    [Fact]
    public void Build_SetsMode_PointsFollowSetOutcomeKeysNotWinDrawLoss()
    {
        var wally = new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = 5, Label = "set", Minutes = null },
            Points = new Dictionary<string, int>
            {
                ["win_3_0"] = 3,
                ["win_3_1"] = 3,
                ["win_3_2"] = 2,
                ["loss_2_3"] = 1,
                ["loss_1_3"] = 0,
                ["loss_0_3"] = 0,
            },
            Tiebreakers = [Tiebreaker.ScoreDifference],
        };

        var contenders = new[] { new Contender(X, "X", null), new Contender(Y, "Y", null) };

        // Consolidated set score, as StandingsCalculator receives it: X won
        // 3 sets to 2.
        var played = new[] { new PlayedMatch(X, Y, 3, 2) };

        var groups = StandingsCalculator.Build(contenders, played, ScoreMode.Sets, wally);

        Row(groups, X).Points.Should().Be(2); // win_3_2
        Row(groups, Y).Points.Should().Be(1); // loss_2_3 — the league pays for taking sets
    }
}
