using AwesomeAssertions;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// Whether a reported score could have happened in a sport, under a given
/// ruleset. Expected violations below are worked out by hand from what a
/// referee would object to — a period reported twice, a set that ends level,
/// a set score nobody could reach a decision on — not by reading the private
/// Inspect* methods and mirroring what they currently do.
/// </summary>
public sealed class ResultPolicyTests
{
    private static Sport CumulativeSport(string label = "tiempo") => new()
    {
        Code = "football",
        Name = "Fútbol",
        PeriodLabel = label,
        DefaultPeriods = 2,
        ScoringUnit = "gol",
        ScoreMode = ScoreMode.Cumulative,
    };

    private static Sport SetsSport(string label = "set") => new()
    {
        Code = "wally",
        Name = "Wally",
        PeriodLabel = label,
        DefaultPeriods = 5,
        ScoringUnit = "punto",
        ScoreMode = ScoreMode.Sets,
    };

    private static MatchRules Cumulative(short configuredPeriods = 2) => new(
        CumulativeSport(),
        new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = configuredPeriods, Label = "tiempo", Minutes = 45 },
            Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
            Tiebreakers = [],
        });

    private static MatchRules Sets(short configuredPeriods = 5) => new(
        SetsSport(),
        new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = configuredPeriods, Label = "set", Minutes = null },
            Points = new Dictionary<string, int>
            {
                ["win_3_0"] = 3, ["loss_0_3"] = 0,
                ["win_3_1"] = 3, ["loss_1_3"] = 0,
                ["win_3_2"] = 3, ["loss_2_3"] = 1,
            },
            Tiebreakers = [],
        });

    private static PeriodScore P(short number, int home, int away) =>
        new() { Period = number, Home = home, Away = away };

    // ---- Structural checks, shared by every sport -----------------------

    [Fact]
    public void Inspect_NoPeriodsReported_IsRejected()
    {
        var violations = ResultPolicy.Inspect(Cumulative(), []);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_APeriodNumberReportedTwice_IsRejected()
    {
        var periods = new[] { P(1, 1, 0), P(1, 0, 1) };

        var violations = ResultPolicy.Inspect(Cumulative(), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_PeriodsSkipANumber_IsRejected()
    {
        // Two periods reported, numbered 1 and 3: period 2 is missing.
        var periods = new[] { P(1, 1, 0), P(3, 0, 1) };

        var violations = ResultPolicy.Inspect(Cumulative(), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_ANegativeScore_IsRejected()
    {
        var periods = new[] { P(1, -1, 0) };

        var violations = ResultPolicy.Inspect(Cumulative(1), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_MalformedNumbering_StopsBeforeAnySportSpecificCheck()
    {
        // Duplicated numbering AND a period count that would also fail the
        // cumulative-mode count check. Only the structural problem should be
        // reported — judging a sequence that is not one is not meaningful.
        var periods = new[] { P(1, 1, 0), P(1, 0, 1), P(1, 2, 0) };

        var violations = ResultPolicy.Inspect(Cumulative(2), periods);

        violations.Should().ContainSingle();
    }

    // ---- Cumulative --------------------------------------------------

    [Fact]
    public void Inspect_Cumulative_ExactlyTheConfiguredPeriodCount_IsAccepted()
    {
        var periods = new[] { P(1, 1, 0), P(2, 2, 1) };

        var violations = ResultPolicy.Inspect(Cumulative(2), periods);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_Cumulative_FewerPeriodsThanConfigured_IsRejected()
    {
        // Ruleset says two halves; only one was reported. Football does not
        // record a match abandoned at half time as a short result — that is
        // a postponed or cancelled state, not a score.
        var periods = new[] { P(1, 1, 0) };

        var violations = ResultPolicy.Inspect(Cumulative(2), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_Cumulative_MorePeriodsThanConfigured_IsRejected()
    {
        var periods = new[] { P(1, 1, 0), P(2, 0, 1), P(3, 1, 1) };

        var violations = ResultPolicy.Inspect(Cumulative(2), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    // ---- Sets ------------------------------------------------------------

    [Fact]
    public void Inspect_Sets_ATiedPeriod_IsRejected()
    {
        // Nothing decides a tied set — it was not finished.
        var periods = new[] { P(1, 25, 25) };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_Sets_NobodyReachedTheDecidingSet_IsRejected()
    {
        // Best of five is won at three. Two sets to one is a match still in
        // progress, not a result.
        var periods = new[] { P(1, 25, 20), P(2, 20, 25), P(3, 25, 18) };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_Sets_WinnerReportedBeyondTheDecidingSet_IsRejected()
    {
        // Best of five is decided at three sets. Reporting a side with four
        // sets is impossible: nothing is played after the third is won.
        var periods = new[] { P(1, 25, 20), P(2, 25, 18), P(3, 25, 22), P(4, 25, 15), P(5, 15, 25) };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_Sets_BothSidesReachTheDecidingSetCount_IsRejected()
    {
        // A 3-3 line under a best-of-five ruleset: a match is decided the
        // moment one side reaches three, so the other side could not have
        // also reached three in the same match.
        var periods = new[]
        {
            P(1, 25, 20), P(2, 20, 25), P(3, 25, 18), P(4, 18, 25), P(5, 25, 20), P(6, 20, 25),
        };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("PeriodScores");
    }

    [Fact]
    public void Inspect_Sets_AValidBestOfFiveFinishingThreeToOne_IsAccepted()
    {
        var periods = new[] { P(1, 25, 20), P(2, 22, 25), P(3, 25, 18), P(4, 25, 20) };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Inspect_Sets_APeriodCountDoesNotHaveToMatchTheConfiguredPeriodCount()
    {
        // Unlike cumulative sports, a set match is not required to use every
        // configured period — it stops at the deciding set, whatever that
        // number is against a ruleset that merely caps it. A best-of-five
        // match that ends 3-0 (three periods, not five) is a normal result.
        var periods = new[] { P(1, 25, 10), P(2, 25, 15), P(3, 25, 20) };

        var violations = ResultPolicy.Inspect(Sets(5), periods);

        violations.Should().BeEmpty();
    }

    // ---- WalkoverScore -----------------------------------------------

    [Fact]
    public void WalkoverScore_RulesetWithNoWalkoverConfigured_IsNull()
    {
        ResultPolicy.WalkoverScore(Cumulative()).Should().BeNull();
    }

    [Fact]
    public void WalkoverScore_RulesetWithAWalkoverConfigured_ReturnsItsWinnerAndLoserScore()
    {
        var baseline = Cumulative();
        var rules = baseline with
        {
            Configuration = baseline.Configuration with
            {
                Walkover = new WalkoverRules { WinnerScore = 3, LoserScore = 0 },
            },
        };

        ResultPolicy.WalkoverScore(rules).Should().Be((3, 0));
    }
}
