using AwesomeAssertions;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Rules;

/// <summary>
/// How long a match takes, worked out from a reglamento's own period rules —
/// never from anything about the sport itself, since <see cref="PeriodRules"/>
/// is all this reads.
/// </summary>
public sealed class MatchDurationTests
{
    private static PeriodRules Football() => new()
    {
        Count = 2,
        Label = "tiempo",
        Minutes = 45,
        BreakMinutes = 15,
    };

    [Fact]
    public void From_TwoHalvesWithABreak_AddsBothHalvesAndTheOneBreakBetweenThem()
    {
        // 45 + 45 + 15 — one break, because there is exactly one gap between
        // two periods, not one per period.
        MatchDuration.From(Football()).Should().Be((short)105);
    }

    [Fact]
    public void From_NoBreakDeclared_TreatsItAsZeroRatherThanFailing()
    {
        // A reglamento written before BreakMinutes existed still adds up to
        // exactly the total it always did.
        var noBreak = Football() with { BreakMinutes = null };

        MatchDuration.From(noBreak).Should().Be((short)90);
    }

    [Fact]
    public void From_OnePeriod_HasNoBreakToAddEvenIfOneWasDeclared()
    {
        // A single-period match (a judged bout) has no gap between periods —
        // Count - 1 is zero, so any BreakMinutes present is never multiplied
        // by anything.
        var onePeriod = new PeriodRules { Count = 1, Label = "actuación", Minutes = 60, BreakMinutes = 10 };

        MatchDuration.From(onePeriod).Should().Be((short)60);
    }

    [Fact]
    public void From_NoClockAndNoEstimateDeclared_IsNull()
    {
        // A set decided purely on score has nothing to add up on its own,
        // and nothing here says how long one runs either — the same
        // "no clock" that RulesetShapeValidator requires BreakMinutes to
        // also be absent for.
        var noClock = new PeriodRules { Count = 3, Label = "set", Minutes = null, BreakMinutes = null };

        MatchDuration.From(noClock).Should().BeNull();
    }

    [Fact]
    public void From_NoClockButEstimateDeclared_ReturnsTheEstimateAsIs()
    {
        // A volleyball set decided purely on score has no clock to compute
        // from, so the reglamento's own declared estimate is the only source
        // of a duration at all — read straight through, no arithmetic.
        var noClockWithEstimate = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 30 };

        MatchDuration.From(noClockWithEstimate).Should().Be((short)30);
    }

    [Fact]
    public void From_ClockedPeriod_IgnoresAnyEstimateEvenIfOnePresent()
    {
        // Computed always wins over declared where both could apply — the
        // shape validator refuses this combination in practice (see
        // RulesetShapeValidatorTests.Validate_EstimatedMinutesDeclaredAlongsideAClock_IsRejected),
        // but MatchDuration itself still has a single, unambiguous answer
        // rather than depending on that validation having already run.
        var clockedWithStrayEstimate = Football() with { EstimatedMinutes = 999 };

        MatchDuration.From(clockedWithStrayEstimate).Should().Be((short)105);
    }

    [Fact]
    public void From_ThreeTimedPeriods_AddsTwoBreaksNotThree()
    {
        // Best-of-three asaltos: three periods, two gaps between them.
        var kyorugi = new PeriodRules { Count = 3, Label = "asalto", Minutes = 2, BreakMinutes = 1 };

        MatchDuration.From(kyorugi).Should().Be((short)8); // 2+2+2 + 1+1
    }
}
