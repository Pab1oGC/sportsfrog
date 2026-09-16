using AwesomeAssertions;
using SportFrog.Api.Features.Rulebook;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Rulebook;

/// <summary>
/// The shape checks a configuration must pass on its own, without the
/// catalog: bounds on numbers, and that nothing required is missing. Written
/// against a football-shaped configuration throughout — which outcomes are
/// legal for which sport is <see cref="RulesetPolicy"/>'s question, not this
/// one's.
/// </summary>
public sealed class RulesetShapeValidatorTests
{
    private static readonly RulesetShapeValidator Validator = new();

    private static RulesetConfiguration Valid() => new()
    {
        Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
        Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
        Tiebreakers = ["score_difference"],
    };

    private static bool HasErrorOn(RulesetConfiguration configuration, string property, string? message = null)
    {
        var result = Validator.Validate(configuration);
        return result.Errors.Any(error =>
            error.PropertyName == property && (message is null || error.ErrorMessage == message));
    }

    [Fact]
    public void Validate_FullyPricedPointsWithATiebreaker_IsValid()
    {
        Validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyPointsWithNoTiebreakers_IsValid()
    {
        // Opting out of a standings table (see RulesetConfiguration.Points'
        // remark) means there is nothing a tiebreaker would ever separate —
        // requiring one anyway would be asking the organizer to solve a
        // problem their reglamento will never have.
        var noTable = Valid() with
        {
            Points = new Dictionary<string, int>(),
            Tiebreakers = [],
        };

        Validator.Validate(noTable).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyPointsButATiebreakerListedAnyway_IsStillValid()
    {
        // Harmless leftover from before Points was cleared, or from a sport
        // switch — not required, but not forbidden either.
        var noTableButListed = Valid() with
        {
            Points = new Dictionary<string, int>(),
            Tiebreakers = ["score_difference"],
        };

        Validator.Validate(noTableButListed).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PricedPointsWithNoTiebreakers_IsRejected()
    {
        // A table that prices outcomes can produce two teams level on
        // points, and without a tiebreaker there is no defined order between
        // them.
        var priceButNoOrder = Valid() with { Tiebreakers = [] };

        HasErrorOn(priceButNoOrder, "Tiebreakers",
                "Se necesita al menos un desempate, o dos equipos igualados en " +
                "puntos no tienen un orden definido.")
            .Should().BeTrue();
    }

    [Fact]
    public void Validate_DuplicateTiebreaker_IsRejected_EvenWithoutATable()
    {
        // Structural nonsense either way: a criterion applied twice never
        // separates anything the first pass did not already separate,
        // whether or not there ends up being a table to apply it to.
        var duplicated = Valid() with
        {
            Points = new Dictionary<string, int>(),
            Tiebreakers = ["score_difference", "score_difference"],
        };

        HasErrorOn(duplicated, "Tiebreakers",
                "Un desempate no puede aparecer dos veces: aplicado una segunda " +
                "vez no separa nada que la primera pasada no haya separado.")
            .Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownTiebreaker_IsRejected_EvenWithoutATable()
    {
        var unknown = Valid() with
        {
            Points = new Dictionary<string, int>(),
            Tiebreakers = ["not_a_real_criterion"],
        };

        var result = Validator.Validate(unknown);

        result.Errors.Should().Contain(error =>
            error.PropertyName == "Tiebreakers" && error.ErrorMessage.StartsWith("Desempates desconocidos."));
    }

    [Fact]
    public void Validate_PointValueOutOfRange_IsRejected_RegardlessOfWhetherATableExists()
    {
        var tooHigh = Valid() with
        {
            Points = new Dictionary<string, int> { ["win"] = 101, ["draw"] = 1, ["loss"] = 0 },
        };

        Validator.Validate(tooHigh).Errors.Should().Contain(error => error.PropertyName == "Points");
    }

    // ---- BreakMinutes ---------------------------------------------------

    [Fact]
    public void Validate_BreakMinutesWithinRangeAlongsideAClock_IsValid()
    {
        var withBreak = Valid() with
        {
            Periods = Valid().Periods with { BreakMinutes = 15 },
        };

        Validator.Validate(withBreak).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoBreakDeclared_IsValid()
    {
        // Absent is "not declared", not an error — MatchDuration.From reads
        // it as zero.
        Validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BreakMinutesOutOfRange_IsRejected()
    {
        var tooLong = Valid() with { Periods = Valid().Periods with { BreakMinutes = 61 } };

        Validator.Validate(tooLong).Errors.Should().Contain(error => error.PropertyName == "Periods.BreakMinutes");
    }

    [Fact]
    public void Validate_BreakMinutesDeclaredWithoutAClock_IsRejected()
    {
        // A set decided purely on score has no clock, and therefore no time
        // between periods to declare either.
        var noClockWithBreak = Valid() with
        {
            Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, BreakMinutes = 5 },
        };

        HasErrorOn(noClockWithBreak, "Periods.BreakMinutes",
                "El descanso entre períodos no aplica donde el período no corre por reloj.")
            .Should().BeTrue();
    }

    // ---- EstimatedMinutes -------------------------------------------------

    [Fact]
    public void Validate_EstimatedMinutesDeclaredWithoutAClock_IsValid()
    {
        var noClockWithEstimate = Valid() with
        {
            Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 30 },
        };

        Validator.Validate(noClockWithEstimate).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoEstimateDeclared_IsValidAtThisLayer()
    {
        // Absent here is not an error — RulesetShapeValidator only checks
        // shape, not whether this particular sport needs one. Whether a
        // clockless sport actually requires it is RulesetPolicy's question
        // (see RulesetPolicyTests.InspectAsync_ClocklessSportWithNoEstimateDeclared_IsRejected).
        Validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EstimatedMinutesOutOfRange_IsRejected()
    {
        var tooLong = Valid() with
        {
            Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 241 },
        };

        Validator.Validate(tooLong).Errors.Should().Contain(error => error.PropertyName == "Periods.EstimatedMinutes");
    }

    [Fact]
    public void Validate_EstimatedMinutesZero_IsRejected()
    {
        // Zero is not a duration anyone plays for — same lower bound as a
        // clocked period's own Minutes.
        var zero = Valid() with
        {
            Periods = new PeriodRules { Count = 3, Label = "set", Minutes = null, EstimatedMinutes = 0 },
        };

        Validator.Validate(zero).Errors.Should().Contain(error => error.PropertyName == "Periods.EstimatedMinutes");
    }

    [Fact]
    public void Validate_EstimatedMinutesDeclaredAlongsideAClock_IsRejected()
    {
        // A clocked period already computes its own duration — declaring an
        // estimate alongside it would be a second, possibly conflicting,
        // answer to the same question.
        var clockedWithEstimate = Valid() with
        {
            Periods = Valid().Periods with { EstimatedMinutes = 30 },
        };

        HasErrorOn(clockedWithEstimate, "Periods.EstimatedMinutes",
                "La duración estimada no aplica donde el período corre por reloj: ahí se calcula sola.")
            .Should().BeTrue();
    }
}
