using FluentValidation;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Checks a configuration for what can be judged without knowing the sport:
/// that the numbers are within reach of a real competition, that nothing
/// required is missing, and that the tiebreakers name criteria the standings
/// can actually compute.
/// </summary>
/// <remarks>
/// Separate from <see cref="RulesetPolicy"/>, and the line between them is
/// whether the catalog has to be consulted. Everything here is true or false
/// on its own, so it is answered without a query; whether three points a win
/// is a sensible thing for this particular sport to say is not, and lives
/// there.
///
/// The split also decides what happens when both could complain. This runs
/// first, and the sport-aware pass is skipped while the structure is still
/// broken — a configuration with no periods would otherwise be reported as
/// pricing the wrong set scores, which is true and useless.
/// </remarks>
internal sealed class RulesetShapeValidator : AbstractValidator<RulesetConfiguration>
{
    /// <summary>
    /// Nine periods is past anything the catalog plays and still short of a
    /// typo; four hours is the same idea for a period length. Bounds exist
    /// here to catch a slipped digit, not to express a rule.
    /// </summary>
    private const short MaximumPeriods = 9;
    private const short MaximumMinutes = 240;

    /// <summary>
    /// A win worth a hundred points is not a competition anyone is running,
    /// and a negative price for an outcome is a sanction rather than a rule
    /// of the table.
    /// </summary>
    private const int MaximumPointValue = 100;

    public RulesetShapeValidator()
    {
        RuleFor(configuration => configuration.Periods)
            .NotNull().WithMessage("The periods rule is required.");

        When(configuration => configuration.Periods is not null, () =>
        {
            RuleFor(configuration => configuration.Periods.Count)
                .InclusiveBetween((short)1, MaximumPeriods)
                .WithMessage($"A match is played in between 1 and {MaximumPeriods} periods.");

            RuleFor(configuration => configuration.Periods.Label)
                .NotEmpty().WithMessage("A period needs a name: half, quarter, set.")
                .MaximumLength(30);

            RuleFor(configuration => configuration.Periods.Minutes)
                .InclusiveBetween((short)1, MaximumMinutes)
                .When(configuration => configuration.Periods.Minutes.HasValue)
                .WithMessage($"A period lasts between 1 and {MaximumMinutes} minutes, or is left " +
                             "unset where it ends on a score instead.");
        });

        RuleFor(configuration => configuration.Points)
            .NotNull().WithMessage("The points rule is required.")
            .Must(points => points.Count > 0)
                .When(configuration => configuration.Points is not null)
                .WithMessage("A ruleset has to say what a result is worth.")
            .Must(points => points.Values.All(value => value is >= 0 and <= MaximumPointValue))
                .When(configuration => configuration.Points is not null)
                .WithMessage($"Each outcome is worth between 0 and {MaximumPointValue} points.");

        RuleFor(configuration => configuration.Tiebreakers)
            .NotNull().WithMessage("The tiebreakers rule is required.");

        When(configuration => configuration.Tiebreakers is not null, () =>
        {
            RuleFor(configuration => configuration.Tiebreakers)
                .Must(tiebreakers => tiebreakers.Count > 0)
                    .WithMessage("At least one tiebreaker is needed, or two teams level on " +
                                 "points have no defined order.")
                .Must(tiebreakers => tiebreakers.Distinct(StringComparer.Ordinal).Count()
                                     == tiebreakers.Count)
                    .WithMessage("A tiebreaker cannot appear twice: applied a second time it " +
                                 "separates nothing the first pass did not.")
                .Must(tiebreakers => tiebreakers.All(Tiebreaker.All.Contains))
                    .WithMessage("Unknown tiebreakers. Available: " +
                                 $"{string.Join(", ", Tiebreaker.All.Order(StringComparer.Ordinal))}.");
        });

        When(configuration => configuration.Walkover is not null, () =>
        {
            RuleFor(configuration => configuration.Walkover!.WinnerScore)
                .GreaterThan(configuration => configuration.Walkover!.LoserScore)
                .WithMessage("A walkover is a win: the side that appeared has to end above " +
                             "the one that did not.");

            RuleFor(configuration => configuration.Walkover!.LoserScore)
                .GreaterThanOrEqualTo((short)0);
        });

        When(configuration => configuration.Metrics is not null, () =>
        {
            RuleFor(configuration => configuration.Metrics!)
                .Must(metrics => metrics.All(code => !string.IsNullOrWhiteSpace(code)))
                    .WithMessage("A metric is named by its code.")
                .Must(metrics => metrics.Distinct(StringComparer.Ordinal).Count() == metrics.Count)
                    .WithMessage("A metric cannot be listed twice.");
        });
    }
}
