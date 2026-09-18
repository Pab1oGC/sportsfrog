using AwesomeAssertions;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// Whether a ruleset configuration makes sense for the sport it claims to be
/// for. Expected violations below are worked out by hand from the sports
/// seeded by the real catalog migration — football is cumulative and plays
/// two halves, wally is sets and best of three — not by reading the private
/// Inspect* methods and mirroring what they currently do.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RulesetPolicyTests(SportFrogDatabaseFixture fixture)
{
    // The real registries, built from the real per-mode implementations —
    // not mocks. All four are pure and DB-free, so this is exactly the
    // wiring Program.cs assembles through the container, exercised without
    // one; only the database read for the sport itself is real.
    private RulesetPolicy Policy() => new(
        fixture.CreateAppContext(),
        new MatchOutcomeRulesRegistry(
            [new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules(), new JudgedMatchOutcomeRules()]),
        new RulesetShapeRulesRegistry(
            [new CumulativeRulesetShape(), new SetsRulesetShape(), new JudgedRulesetShape()]));

    // Best of three: won at two. EstimatedMinutes is required here, not
    // optional decoration — wally has no clock (see the real catalog row
    // AddSportPeriodHasClock seeds), so InspectDuration rejects a wally
    // ruleset without one.
    private static RulesetConfiguration ValidWally() => new()
    {
        Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 30 },
        Points = new Dictionary<string, int>
        {
            ["win_2_0"] = 3, ["loss_0_2"] = 0,
            ["win_2_1"] = 3, ["loss_1_2"] = 1,
        },
        Tiebreakers = ["score_difference"],
    };

    // Best of three asaltos: won at two. Kyorugi's real catalog row, seeded
    // by AddTaekwondoCatalogFoundations — unlike ValidWally, which exercises
    // the sets mechanism against a hand-built shape, this proves that row's
    // own data (score_mode, the point/penalty metrics) integrates correctly.
    private static RulesetConfiguration ValidKyorugi() => new()
    {
        Periods = new PeriodRules { Count = 3, Label = "asalto", Minutes = null },
        Points = new Dictionary<string, int>
        {
            ["win_2_0"] = 3, ["loss_0_2"] = 0,
            ["win_2_1"] = 3, ["loss_1_2"] = 1,
        },
        Tiebreakers = ["score_difference"],
    };

    // One performance a side, decided by judges: won at whatever score is
    // higher, never level. Against the real taekwondo_poomsae catalog row
    // seeded by SeedPoomsaeCatalog — also clockless, same as wally, so this
    // needs EstimatedMinutes for the same reason ValidWally does.
    private static RulesetConfiguration ValidPoomsae() => new()
    {
        Periods = new PeriodRules { Count = 1, Label = "actuación", Minutes = null, EstimatedMinutes = 5 },
        Points = new Dictionary<string, int> { ["win"] = 3, ["loss"] = 0 },
        Tiebreakers = [],
    };

    private static RulesetConfiguration ValidFootball() => new()
    {
        Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
        Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
        Tiebreakers = ["score_difference"],
    };

    [Fact]
    public async Task InspectAsync_UnknownSport_IsTheOnlyViolationReported()
    {
        var violations = await Policy().InspectAsync(
            "not-a-real-sport", ValidFootball(), CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("SportCode");
    }

    [Fact]
    public async Task InspectAsync_ValidCumulativeRuleset_IsAccepted()
    {
        var violations = await Policy().InspectAsync("football", ValidFootball(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_ValidSetsRuleset_IsAccepted()
    {
        var violations = await Policy().InspectAsync("wally", ValidWally(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    // ---- Kyorugi (real catalog sport, best of three) --------------------

    [Fact]
    public async Task InspectAsync_ValidKyorugiRuleset_IsAccepted()
    {
        var violations = await Policy().InspectAsync(
            "taekwondo_kyorugi", ValidKyorugi(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_KyorugiWalkoverWinnerScoreNotEqualToTheDecidingAsalto_IsRejected()
    {
        // Won at two asaltos, same as any other best-of-three sets sport —
        // three is impossible.
        var wrongWinnerScore = ValidKyorugi() with
        {
            Walkover = new WalkoverRules { WinnerScore = 3, LoserScore = 0 },
        };

        var violations = await Policy().InspectAsync(
            "taekwondo_kyorugi", wrongWinnerScore, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Walkover.WinnerScore");
    }

    [Fact]
    public async Task InspectAsync_KyorugiUnknownMetric_IsRejectedAgainstTheCatalogsOwnPointAndPenalty()
    {
        // Both real metrics stay listed here on purpose: since
        // AddTaekwondoKyorugiScoringEvents, point and penalty both affect
        // the score, and leaving either out is its own violation (see
        // InspectAsync_KyorugiExcludingAScoringMetric_IsRejected below) —
        // this test is about the unknown one, so the other two stay valid.
        var unknownMetric = ValidKyorugi() with { Metrics = ["point", "penalty", "not_a_real_metric"] };

        var violations = await Policy().InspectAsync(
            "taekwondo_kyorugi", unknownMetric, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Metrics");
    }

    [Fact]
    public async Task InspectAsync_KyorugiExcludingAScoringMetric_IsRejected()
    {
        // Unlike wally or volleyball, kyorugi's point and gam-jeom do
        // decide the asalto being fought (AddTaekwondoKyorugiScoringEvents)
        // — same rule InspectAsync_AScoringMetricLeftOutOfACumulativeSport_
        // IsRejected already covers for football's goal, exercised here for
        // the one sets-mode sport it actually applies to.
        var missingScoringMetric = ValidKyorugi() with { Metrics = ["point"] };

        var violations = await Policy().InspectAsync(
            "taekwondo_kyorugi", missingScoringMetric, CancellationToken.None);

        violations.Should().Contain(violation => violation.Property == "Config.Metrics");
    }

    // ---- Poomsae (real catalog sport, one judged performance) -----------

    [Fact]
    public async Task InspectAsync_ValidPoomsaeRuleset_IsAccepted()
    {
        var violations = await Policy().InspectAsync(
            "taekwondo_poomsae", ValidPoomsae(), CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_PoomsaeWithMoreThanOnePeriod_IsRejected()
    {
        // A judged bout is one performance a side, not a best-of-anything —
        // this is the constraint SetsRulesetShape has no equivalent for.
        var twoPeriods = ValidPoomsae() with
        {
            Periods = new PeriodRules { Count = 2, Label = "actuación", Minutes = null },
        };

        var violations = await Policy().InspectAsync(
            "taekwondo_poomsae", twoPeriods, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Periods.Count");
    }

    [Fact]
    public async Task InspectAsync_PoomsaePricingADraw_IsRejected()
    {
        // Unlike a cumulative sport, a judged bout never offers a draw even
        // as optional — the judges resolve a tie before a result is ever
        // recorded, so a ruleset cannot price a result that can't happen.
        var withDraw = ValidPoomsae() with
        {
            Points = new Dictionary<string, int> { ["win"] = 3, ["loss"] = 0, ["draw"] = 1 },
        };

        var violations = await Policy().InspectAsync(
            "taekwondo_poomsae", withDraw, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Points");
    }

    [Fact]
    public async Task InspectAsync_PoomsaeWalkover_IsNeverValidatedAgainstAScorelineRule()
    {
        // Same as a cumulative sport: a judged score is never read against a
        // scoreline the way a set is, so any pair of numbers is accepted.
        var anyNumbersAtAll = ValidPoomsae() with
        {
            Walkover = new WalkoverRules { WinnerScore = 1, LoserScore = 0 },
        };

        var violations = await Policy().InspectAsync(
            "taekwondo_poomsae", anyNumbersAtAll, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    // ---- Periods -----------------------------------------------------

    [Fact]
    public async Task InspectAsync_SetsSportWithAnEvenPeriodCount_IsRejected()
    {
        // Wally is best of three by default; an even count leaves a match
        // that cannot be decided.
        var evenCount = ValidWally() with { Periods = new PeriodRules { Count = 4, Label = "set", Minutes = null } };

        var violations = await Policy().InspectAsync("wally", evenCount, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Periods.Count");
    }

    [Fact]
    public async Task InspectAsync_CumulativeSportWithAnEvenPeriodCount_IsNotRejectedOnThatGround()
    {
        // Nothing about a cumulative match requires an odd period count —
        // the constraint is specific to sets, where somebody has to take a
        // deciding one.
        var evenHalves = ValidFootball() with
        {
            Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
        };

        var violations = await Policy().InspectAsync("football", evenHalves, CancellationToken.None);

        violations.Should().NotContain(violation => violation.Property == "Config.Periods.Count");
    }

    [Fact]
    public async Task InspectAsync_UnusablePeriodCount_SkipsPointsAndWalkoverRatherThanCompoundingTheReport()
    {
        // An even sets count, paired with points and a walkover that are
        // ALSO wrong. Only the periods problem should surface — the
        // outcomes a match can finish on cannot be derived from an unusable
        // period count, so judging them would be answering a question that
        // has not been asked yet.
        var brokenEverything = new RulesetConfiguration
        {
            Periods = new PeriodRules { Count = 4, Label = "set", Minutes = null }, // even: wrong
            Points = new Dictionary<string, int> { ["win"] = 3 }, // wrong shape for sets mode too
            Tiebreakers = [],
            Walkover = new WalkoverRules { WinnerScore = 99, LoserScore = 99 }, // nonsense either way
        };

        var violations = await Policy().InspectAsync("wally", brokenEverything, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Periods.Count");
    }

    // ---- Duration --------------------------------------------------------

    [Fact]
    public async Task InspectAsync_ClocklessSportWithNoEstimateDeclared_IsRejected()
    {
        // Wally has no clock at all (the real catalog row) — nothing lets
        // MatchDuration.From compute a duration for it, so an estimate has
        // to be declared by hand or the calendar has nothing to schedule
        // against.
        var noEstimate = ValidWally() with { Periods = ValidWally().Periods with { EstimatedMinutes = null } };

        var violations = await Policy().InspectAsync("wally", noEstimate, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Periods.EstimatedMinutes");
    }

    [Fact]
    public async Task InspectAsync_ClockedSportWithNoPeriodMinutesAndNoEstimate_IsNotRejectedOnDurationGrounds()
    {
        // Kyorugi's own catalog row has a clock (PeriodHasClock), even though
        // this particular ruleset leaves Minutes unset — InspectDuration
        // reads the sport's own flag, not whether this ruleset happened to
        // fill Minutes in, so a clocked sport is never asked for an estimate.
        var violations = await Policy().InspectAsync("taekwondo_kyorugi", ValidKyorugi(), CancellationToken.None);

        violations.Should().NotContain(violation => violation.Property == "Config.Periods.EstimatedMinutes");
    }

    // ---- Points --------------------------------------------------------

    [Fact]
    public async Task InspectAsync_CumulativeRulesetMissingLoss_IsRejected()
    {
        var missingLoss = ValidFootball() with
        {
            Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1 },
        };

        var violations = await Policy().InspectAsync("football", missingLoss, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Points");
    }

    [Fact]
    public async Task InspectAsync_CumulativeRulesetPricingASetsStyleOutcome_IsRejected()
    {
        // "win_3_0" cannot happen in a cumulative sport: nothing here plays
        // in sets.
        var wrongShape = ValidFootball() with
        {
            Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0, ["win_3_0"] = 5 },
        };

        var violations = await Policy().InspectAsync("football", wrongShape, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Points");
    }

    [Fact]
    public async Task InspectAsync_SetsRulesetMissingARequiredScoreline_IsRejected()
    {
        // Best of three needs all four scorelines priced; loss_1_2 is left
        // out.
        var incomplete = ValidWally() with
        {
            Points = new Dictionary<string, int> { ["win_2_0"] = 3, ["loss_0_2"] = 0, ["win_2_1"] = 3 },
        };

        var violations = await Policy().InspectAsync("wally", incomplete, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Points");
    }

    [Fact]
    public async Task InspectAsync_CumulativeRulesetWithNoDrawPriced_IsAccepted()
    {
        // The draw is optional under a cumulative score — a competition that
        // plays to a decision (extra time, shootout) never needs it priced.
        var noDraw = ValidFootball() with { Points = new Dictionary<string, int> { ["win"] = 3, ["loss"] = 0 } };

        var violations = await Policy().InspectAsync("football", noDraw, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CumulativeRulesetWithEmptyPoints_IsAcceptedAsOptingOutOfAStandingsTable()
    {
        // Empty is not "forgot to fill it in" — it is a ruleset written for a
        // competition that will never build a table from it (a straight
        // knockout draw), and StandingsCalculator already prices an unlisted
        // outcome at zero, so leaving all of them out changes nothing a
        // table would ever read.
        var noTable = ValidFootball() with { Points = new Dictionary<string, int>() };

        var violations = await Policy().InspectAsync("football", noTable, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_SetsRulesetWithEmptyPoints_IsAcceptedAsOptingOutOfAStandingsTable()
    {
        var noTable = ValidWally() with { Points = new Dictionary<string, int>() };

        var violations = await Policy().InspectAsync("wally", noTable, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_KyorugiRulesetWithEmptyPoints_IsAcceptedAsOptingOutOfAStandingsTable()
    {
        // Straight elimination Kyorugi: who advances is decided by
        // MatchWinner, never by a table, so the organizer should not have to
        // invent a price for a scoreline nothing will ever add up.
        var noTable = ValidKyorugi() with { Points = new Dictionary<string, int>() };

        var violations = await Policy().InspectAsync("taekwondo_kyorugi", noTable, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_PartiallyPricedPoints_IsStillRejected_EmptyIsTheOnlyValidWayToOptOut()
    {
        // Pricing a win but leaving the loss out is not "half opting out" —
        // it almost certainly means a table was intended and the reglamento
        // was left incomplete. Only fully empty is read as a deliberate
        // choice; see InspectAsync_CumulativeRulesetMissingLoss_IsRejected
        // above, which already covers this shape but is restated here next
        // to the opt-out tests so the boundary between the two is explicit.
        var halfPriced = ValidFootball() with { Points = new Dictionary<string, int> { ["win"] = 3 } };

        var violations = await Policy().InspectAsync("football", halfPriced, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Points");
    }

    // ---- Metrics -------------------------------------------------------

    [Fact]
    public async Task InspectAsync_MetricsNotBelongingToTheSport_IsRejected()
    {
        var unknownMetric = ValidFootball() with { Metrics = ["goal", "not_a_real_metric"] };

        var violations = await Policy().InspectAsync("football", unknownMetric, CancellationToken.None);

        violations.Should().Contain(violation => violation.Property == "Config.Metrics");
    }

    [Fact]
    public async Task InspectAsync_AScoringMetricLeftOutOfACumulativeSport_IsRejected()
    {
        // Football's score is the sum of "goal" and "own_goal" events. A
        // ruleset that tracks only assists cannot produce a score at all.
        var missingScoringMetric = ValidFootball() with { Metrics = ["assist"] };

        var violations = await Policy().InspectAsync("football", missingScoringMetric, CancellationToken.None);

        violations.Should().Contain(violation => violation.Property == "Config.Metrics");
    }

    [Fact]
    public async Task InspectAsync_MetricsAbsent_MeansEveryMetricAndIsAccepted()
    {
        var noMetricsListed = ValidFootball() with { Metrics = null };

        var violations = await Policy().InspectAsync("football", noMetricsListed, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_SetsSportHasNoScoringMetricToRequire()
    {
        // Under sets, the result comes from periods won, not from summing
        // events — the catalog prices no wally metric as affecting the
        // score, so leaving all of them out is a valid selection.
        var noMetricsAtAll = ValidWally() with { Metrics = [] };

        var violations = await Policy().InspectAsync("wally", noMetricsAtAll, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    // ---- Walkover ------------------------------------------------------

    [Fact]
    public async Task InspectAsync_NoWalkoverConfigured_IsAcceptedRegardlessOfMode()
    {
        var violations = await Policy().InspectAsync(
            "wally", ValidWally() with { Walkover = null }, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_SetsWalkoverWinnerScoreNotEqualToTheDecidingSet_IsRejected()
    {
        // Best of three is won at two, so a walkover has to award exactly
        // two — not one, not three.
        var wrongWinnerScore = ValidWally() with
        {
            Walkover = new WalkoverRules { WinnerScore = 3, LoserScore = 0 },
        };

        var violations = await Policy().InspectAsync("wally", wrongWinnerScore, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Walkover.WinnerScore");
    }

    [Fact]
    public async Task InspectAsync_SetsWalkoverLoserScoreReachingTheDecidingSet_IsRejected()
    {
        var loserTooHigh = ValidWally() with
        {
            Walkover = new WalkoverRules { WinnerScore = 2, LoserScore = 2 },
        };

        var violations = await Policy().InspectAsync("wally", loserTooHigh, CancellationToken.None);

        violations.Should().ContainSingle();
        violations[0].Property.Should().Be("Config.Walkover.LoserScore");
    }

    [Fact]
    public async Task InspectAsync_ValidSetsWalkover_IsAccepted()
    {
        var validWalkover = ValidWally() with
        {
            Walkover = new WalkoverRules { WinnerScore = 2, LoserScore = 0 },
        };

        var violations = await Policy().InspectAsync("wally", validWalkover, CancellationToken.None);

        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task InspectAsync_CumulativeWalkover_IsNeverValidatedAgainstAScorelineRule()
    {
        // The scoreline constraint only makes sense where the score is
        // periods won. Under a cumulative sport any pair of numbers is
        // accepted here, however lopsided — including one where the
        // "winner" is credited fewer than the "loser".
        var anyNumbersAtAll = ValidFootball() with
        {
            Walkover = new WalkoverRules { WinnerScore = 0, LoserScore = 5 },
        };

        var violations = await Policy().InspectAsync("football", anyNumbersAtAll, CancellationToken.None);

        violations.Should().BeEmpty();
    }
}
