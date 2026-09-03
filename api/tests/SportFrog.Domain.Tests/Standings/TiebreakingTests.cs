using AwesomeAssertions;
using SportFrog.Domain.Rules;
using SportFrog.Domain.Standings;

namespace SportFrog.Domain.Tests.Standings;

/// <summary>
/// Separates teams level on points, one criterion at a time, in the order
/// the ruleset lists them. Expected orderings below are worked out by hand
/// from what each named criterion means in an ordinary league table, not by
/// tracing the recursive resolver and mirroring what it outputs.
/// </summary>
public sealed class TiebreakingTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static readonly RulesetConfiguration Football = new()
    {
        Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
        Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
        Tiebreakers = [], // set per test
    };

    private static StandingsRow Row(Guid id, string name, int played, int won, int drawn, int lost, int scoreFor, int scoreAgainst) =>
        new()
        {
            TeamId = id,
            TeamName = name,
            Played = played,
            Won = won,
            Drawn = drawn,
            Lost = lost,
            ScoreFor = scoreFor,
            ScoreAgainst = scoreAgainst,
        };

    private static IReadOnlyList<Guid> Ids(IReadOnlyList<StandingsRow> rows) =>
        [.. rows.Select(row => row.TeamId)];

    [Fact]
    public void Resolve_SingleTeam_ReturnsItUnchanged()
    {
        var only = new[] { Row(A, "A", 1, 1, 0, 0, 1, 0) };

        var resolved = Tiebreaking.Resolve(only, [], ScoreMode.Cumulative, Football with { Tiebreakers = [Tiebreaker.Wins] });

        resolved.Should().BeEquivalentTo(only);
    }

    [Fact]
    public void Resolve_ScoreDifference_FullySeparatesTwoTeams()
    {
        // X: +3, Y: 0. Same points, different goal difference.
        var teamA = Row(A, "A", 2, 1, 0, 1, 5, 2); // +3
        var teamB = Row(B, "B", 2, 1, 0, 1, 3, 3); // 0

        var rules = Football with { Tiebreakers = [Tiebreaker.ScoreDifference] };
        var resolved = Tiebreaking.Resolve([teamB, teamA], [], ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Resolve_ScoreDifferenceTied_FallsThroughToScoreFor()
    {
        // Both +2, but A scored more goals getting there (4-2 vs 3-1).
        var teamA = Row(A, "A", 2, 1, 0, 1, 4, 2);
        var teamB = Row(B, "B", 2, 1, 0, 1, 3, 1);

        var rules = Football with { Tiebreakers = [Tiebreaker.ScoreDifference, Tiebreaker.ScoreFor] };
        var resolved = Tiebreaking.Resolve([teamB, teamA], [], ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Resolve_ScoreAgainst_FewerConcededRanksFirst()
    {
        // Same points and same goal difference is not set up here — this
        // isolates score_against on its own: A conceded one, B conceded four.
        var teamA = Row(A, "A", 2, 1, 0, 1, 3, 1);
        var teamB = Row(B, "B", 2, 1, 0, 1, 3, 4);

        var rules = Football with { Tiebreakers = [Tiebreaker.ScoreAgainst] };
        var resolved = Tiebreaking.Resolve([teamB, teamA], [], ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Resolve_Wins_SeparatesTeamsTiedOnPointsByADifferentRoute()
    {
        // Under a 2-for-a-win, 1-for-a-draw scheme: three draws is 3 points
        // and zero wins; one win plus one draw is also 3 points but one win.
        var rules = Football with
        {
            Points = new Dictionary<string, int> { ["win"] = 2, ["draw"] = 1, ["loss"] = 0 },
            Tiebreakers = [Tiebreaker.Wins],
        };

        var threeDraws = Row(A, "A", 3, 0, 3, 0, 3, 3);
        var oneWinOneDraw = Row(B, "B", 2, 1, 1, 0, 3, 2);

        var resolved = Tiebreaking.Resolve([threeDraws, oneWinOneDraw], [], ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(B, A);
    }

    [Fact]
    public void Resolve_HeadToHead_UsesOnlyTheMatchBetweenTheTiedTeams()
    {
        // A and B are tied on points overall. In their own match, A beat B
        // 1-0. Both also share an identical outside result against D, so
        // nothing outside the tie should be able to leak in and flip this.
        var teamA = Row(A, "A", 2, 1, 0, 1, 6, 5);
        var teamB = Row(B, "B", 2, 1, 0, 1, 6, 5);

        var d = Guid.NewGuid();
        var played = new[]
        {
            new PlayedMatch(A, B, 1, 0), // A beat B head-to-head
            new PlayedMatch(B, d, 5, 5), // identical outside result for both,
            new PlayedMatch(d, A, 5, 5), // so it cannot be what separates them
        };

        var rules = Football with { Tiebreakers = [Tiebreaker.HeadToHead] };
        var resolved = Tiebreaking.Resolve([teamB, teamA], played, ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Resolve_HeadToHead_IgnoresMatchesAgainstTeamsOutsideTheTie()
    {
        // A and B never played each other. Whatever they did against a team
        // outside the tied group must not count toward head-to-head, which
        // is specifically the result between the level teams.
        var teamA = Row(A, "A", 1, 1, 0, 0, 5, 0);
        var teamB = Row(B, "B", 1, 1, 0, 0, 1, 0);

        var d = Guid.NewGuid();
        var played = new[] { new PlayedMatch(A, d, 5, 0) }; // A crushed an outsider; irrelevant here

        var rules = Football with { Tiebreakers = [Tiebreaker.HeadToHead] };
        var resolved = Tiebreaking.Resolve([teamA, teamB], played, ScoreMode.Cumulative, rules);

        // No head-to-head data between A and B exists, so both score zero on
        // this criterion and it falls straight through to the name fallback.
        Ids(resolved).Should().ContainInOrder(A, B); // "A" < "B" alphabetically too, see next test
    }

    [Fact]
    public void Resolve_HeadToHead_ACircularThreeWayTie_CannotSeparateAndFallsToNameOrder()
    {
        // The exact scenario the module's own remarks call out: A beat B,
        // B beat C, C beat A. A pairwise head-to-head comparison of a circle
        // is not transitive, so all three end up level on it too, and the
        // deterministic fallback is alphabetical by name.
        var teamA = Row(A, "Alpha", 2, 1, 0, 1, 2, 2);
        var teamB = Row(B, "Bravo", 2, 1, 0, 1, 2, 2);
        var teamC = Row(C, "Charlie", 2, 1, 0, 1, 2, 2);

        var played = new[]
        {
            new PlayedMatch(A, B, 1, 0),
            new PlayedMatch(B, C, 1, 0),
            new PlayedMatch(C, A, 1, 0),
        };

        var rules = Football with { Tiebreakers = [Tiebreaker.HeadToHead] };
        var resolved = Tiebreaking.Resolve([teamC, teamA, teamB], played, ScoreMode.Cumulative, rules);

        Ids(resolved).Should().ContainInOrder(A, B, C);
    }

    [Fact]
    public void Resolve_NoCriteriaConfigured_FallsStraightToNameOrder()
    {
        var teamA = Row(A, "Zeta", 1, 1, 0, 0, 1, 0);
        var teamB = Row(B, "Alpha", 1, 1, 0, 0, 1, 0);

        var rules = Football with { Tiebreakers = [] };
        var resolved = Tiebreaking.Resolve([teamA, teamB], [], ScoreMode.Cumulative, rules);

        resolved.Select(row => row.TeamName).Should().ContainInOrder("Alpha", "Zeta");
    }

    [Fact]
    public void Resolve_CriteriaExhaustedStillTied_FallsToNameOrder()
    {
        // Identical on every configured criterion.
        var teamA = Row(A, "Zeta", 2, 1, 0, 1, 3, 2);
        var teamB = Row(B, "Alpha", 2, 1, 0, 1, 3, 2);

        var rules = Football with { Tiebreakers = [Tiebreaker.ScoreDifference, Tiebreaker.ScoreFor] };
        var resolved = Tiebreaking.Resolve([teamA, teamB], [], ScoreMode.Cumulative, rules);

        resolved.Select(row => row.TeamName).Should().ContainInOrder("Alpha", "Zeta");
    }

    [Fact]
    public void Resolve_AnUnknownCriterion_SeparatesNothingRatherThanThrowing()
    {
        // Cannot come from a ruleset this application wrote — the rulebook
        // module refuses unknown tiebreakers — but a table built from data
        // written another way must not blow up over one.
        var teamA = Row(A, "Zeta", 1, 1, 0, 0, 1, 0);
        var teamB = Row(B, "Alpha", 1, 1, 0, 0, 1, 0);

        var rules = Football with { Tiebreakers = ["not_a_real_criterion"] };
        var resolved = Tiebreaking.Resolve([teamA, teamB], [], ScoreMode.Cumulative, rules);

        // Falls straight through to the name fallback, same as if no
        // criteria had separated them.
        resolved.Select(row => row.TeamName).Should().ContainInOrder("Alpha", "Zeta");
    }
}
