using AwesomeAssertions;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// What the catalog hands the client for one sport — the capabilities a
/// frontend needs to render the right form and the right buttons without
/// parsing <c>scoreMode</c> itself. Expected outcome keys below are worked
/// out by hand from the sport's own default period count, the same way
/// MatchOutcomesTests does it — not by reading RequiredOutcomes and mirroring
/// what it returns.
/// </summary>
public sealed class ReadSportsTests
{
    private static readonly IMatchOutcomeRulesRegistry OutcomeRules =
        new MatchOutcomeRulesRegistry(
            [new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules(), new JudgedMatchOutcomeRules()]);

    private static Sport Football() => new()
    {
        Code = "football",
        Name = "Fútbol",
        PeriodLabel = "tiempo",
        DefaultPeriods = 2,
        DefaultMinutes = 45,
        DefaultBreakMinutes = 15,
        ScoringUnit = "gol",
        ScoreMode = ScoreMode.Cumulative,
        Metrics =
        [
            new SportMetric { SportCode = "football", Code = "goal", Label = "Gol", AffectsScore = true, IsRankable = true, DisplayOrder = 1 },
            new SportMetric { SportCode = "football", Code = "assist", Label = "Asistencia", AffectsScore = false, IsRankable = true, DisplayOrder = 2 },
        ],
    };

    private static Sport Wally() => new()
    {
        Code = "wally",
        Name = "Wally",
        PeriodLabel = "set",
        DefaultPeriods = 3,
        PeriodHasClock = false,
        ScoringUnit = "punto",
        ScoreMode = ScoreMode.Sets,
        Metrics =
        [
            new SportMetric { SportCode = "wally", Code = "point", Label = "Punto", AffectsScore = false, IsRankable = true, DisplayOrder = 1 },
        ],
    };

    // Taekwondo Kyorugi, shape-only: sets-mode like Wally, but an asalto
    // still runs on a two-minute clock — the case that disproves "played in
    // sets" and "has no clock" are the same fact.
    private static Sport Kyorugi() => new()
    {
        Code = "taekwondo_kyorugi",
        Name = "Taekwondo (Kyorugi)",
        PeriodLabel = "asalto",
        DefaultPeriods = 3,
        PeriodHasClock = true,
        DefaultMinutes = 2,
        DefaultBreakMinutes = 1,
        ScoringUnit = "punto",
        ScoreMode = ScoreMode.Sets,
        IsIndividual = true,
    };

    [Fact]
    public void Project_CumulativeSport_ExposesWinLossAsRequiredAndDrawAsOptional()
    {
        var summary = ReadSports.Project(Football(), OutcomeRules);

        summary.ScoreMode.Should().Be("cumulative");
        summary.IsPlayedInSets.Should().BeFalse();
        summary.RequiredOutcomes.Should().BeEquivalentTo(["win", "loss"]);
        summary.OptionalOutcomes.Should().BeEquivalentTo(["draw"]);
    }

    [Fact]
    public void Project_SetsSport_ExposesEveryScorelineOfItsDefaultBestOfAndNoOptionalOutcome()
    {
        // Wally defaults to best of three: won at two, so 2-0 and 2-1 from
        // both sides — four required keys, none optional.
        var summary = ReadSports.Project(Wally(), OutcomeRules);

        summary.ScoreMode.Should().Be("sets");
        summary.IsPlayedInSets.Should().BeTrue();
        summary.RequiredOutcomes.Should().BeEquivalentTo(["win_2_0", "loss_0_2", "win_2_1", "loss_1_2"]);
        summary.OptionalOutcomes.Should().BeEmpty();
    }

    private static Sport Poomsae() => new()
    {
        Code = "taekwondo_poomsae",
        Name = "Taekwondo (Poomsae)",
        PeriodLabel = "actuación",
        DefaultPeriods = 1,
        ScoringUnit = "punto",
        ScoreMode = ScoreMode.Judged,
        IsIndividual = true,
    };

    [Fact]
    public void Project_JudgedSport_ExposesWinLossAsRequiredAndNoOptionalOutcome()
    {
        // Unlike a cumulative sport, which offers a draw as optional, a
        // judged bout never can: the judges resolve a tie before a result
        // reaches here at all.
        var summary = ReadSports.Project(Poomsae(), OutcomeRules);

        summary.ScoreMode.Should().Be("judged");
        summary.IsPlayedInSets.Should().BeFalse();
        summary.RequiredOutcomes.Should().BeEquivalentTo(["win", "loss"]);
        summary.OptionalOutcomes.Should().BeEmpty();
    }

    [Fact]
    public void Project_TeamSport_IsNotIndividual()
    {
        ReadSports.Project(Football(), OutcomeRules).IsIndividual.Should().BeFalse();
    }

    [Fact]
    public void Project_IndividualSport_ExposesIt()
    {
        ReadSports.Project(Kyorugi(), OutcomeRules).IsIndividual.Should().BeTrue();
    }

    [Fact]
    public void Project_PreservesMetricsInDisplayOrder()
    {
        var summary = ReadSports.Project(Football(), OutcomeRules);

        summary.Metrics.Select(metric => metric.Code).Should().ContainInOrder("goal", "assist");
    }

    [Fact]
    public void Project_MetricSummary_CarriesWhetherEachMetricAffectsTheScore()
    {
        var summary = ReadSports.Project(Football(), OutcomeRules);

        summary.Metrics.Single(metric => metric.Code == "goal").AffectsScore.Should().BeTrue();
        summary.Metrics.Single(metric => metric.Code == "assist").AffectsScore.Should().BeFalse();
    }

    [Fact]
    public void Project_CumulativeSport_CarriesItsStandardClockLength()
    {
        ReadSports.Project(Football(), OutcomeRules).DefaultMinutes.Should().Be((short)45);
    }

    [Fact]
    public void Project_ClocklessSetsSport_HasNoStandardClockLengthToSuggest()
    {
        // Wally's sets end on a score, not a clock — nothing to prefill a
        // reglamento form with.
        ReadSports.Project(Wally(), OutcomeRules).DefaultMinutes.Should().BeNull();
    }

    [Fact]
    public void Project_ClocklessSetsSport_ExposesPeriodHasClockAsFalse()
    {
        ReadSports.Project(Wally(), OutcomeRules).PeriodHasClock.Should().BeFalse();
    }

    [Fact]
    public void Project_TimedSetsSport_ExposesPeriodHasClockAsTrue()
    {
        // Kyorugi is decided by periods won same as Wally, but an asalto
        // still runs on a clock — the two facts are independent, and a
        // client checks this one, not IsPlayedInSets, before it decides
        // whether to show a clock-length field.
        ReadSports.Project(Kyorugi(), OutcomeRules).PeriodHasClock.Should().BeTrue();
    }

    [Fact]
    public void Project_TimedSetsSport_CarriesItsStandardClockLength()
    {
        // The bug this guards against: an earlier version derived "has a
        // clock" from ScoreMode == Sets and left every sets-mode sport
        // clockless, Kyorugi included — which is wrong, an asalto runs two
        // minutes on a clock same as a football half.
        var summary = ReadSports.Project(Kyorugi(), OutcomeRules);

        summary.IsPlayedInSets.Should().BeTrue();
        summary.DefaultMinutes.Should().Be((short)2);
    }

    [Fact]
    public void Project_CumulativeSport_CarriesItsStandardBreakLength()
    {
        ReadSports.Project(Football(), OutcomeRules).DefaultBreakMinutes.Should().Be((short)15);
    }

    [Fact]
    public void Project_ClocklessSetsSport_HasNoStandardBreakLengthToSuggest()
    {
        ReadSports.Project(Wally(), OutcomeRules).DefaultBreakMinutes.Should().BeNull();
    }
}
