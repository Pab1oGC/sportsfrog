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
            .NotNull().WithMessage("La regla de períodos es obligatoria.");

        When(configuration => configuration.Periods is not null, () =>
        {
            RuleFor(configuration => configuration.Periods.Count)
                .InclusiveBetween((short)1, MaximumPeriods)
                .WithMessage($"Un partido se juega en entre 1 y {MaximumPeriods} períodos.");

            RuleFor(configuration => configuration.Periods.Label)
                .NotEmpty().WithMessage("Un período necesita un nombre: tiempo, cuarto, set.")
                .MaximumLength(30);

            RuleFor(configuration => configuration.Periods.Minutes)
                .InclusiveBetween((short)1, MaximumMinutes)
                .When(configuration => configuration.Periods.Minutes.HasValue)
                .WithMessage($"Un período dura entre 1 y {MaximumMinutes} minutos, o se deja sin " +
                             "definir donde termina por marcador en lugar de por reloj.");
        });

        RuleFor(configuration => configuration.Points)
            .NotNull().WithMessage("La regla de puntos es obligatoria.")
            .Must(points => points.Count > 0)
                .When(configuration => configuration.Points is not null)
                .WithMessage("Un reglamento tiene que decir cuánto vale un resultado.")
            .Must(points => points.Values.All(value => value is >= 0 and <= MaximumPointValue))
                .When(configuration => configuration.Points is not null)
                .WithMessage($"Cada desenlace vale entre 0 y {MaximumPointValue} puntos.");

        RuleFor(configuration => configuration.Tiebreakers)
            .NotNull().WithMessage("La regla de desempates es obligatoria.");

        When(configuration => configuration.Tiebreakers is not null, () =>
        {
            RuleFor(configuration => configuration.Tiebreakers)
                .Must(tiebreakers => tiebreakers.Count > 0)
                    .WithMessage("Se necesita al menos un desempate, o dos equipos igualados en " +
                                 "puntos no tienen un orden definido.")
                .Must(tiebreakers => tiebreakers.Distinct(StringComparer.Ordinal).Count()
                                     == tiebreakers.Count)
                    .WithMessage("Un desempate no puede aparecer dos veces: aplicado una segunda " +
                                 "vez no separa nada que la primera pasada no haya separado.")
                .Must(tiebreakers => tiebreakers.All(Tiebreaker.All.Contains))
                    .WithMessage("Desempates desconocidos. Disponibles: " +
                                 $"{string.Join(", ", Tiebreaker.All.Order(StringComparer.Ordinal))}.");
        });

        When(configuration => configuration.Walkover is not null, () =>
        {
            RuleFor(configuration => configuration.Walkover!.WinnerScore)
                .GreaterThan(configuration => configuration.Walkover!.LoserScore)
                .WithMessage("Un walkover es una victoria: el lado que se presentó tiene que " +
                             "terminar por encima del que no.");

            RuleFor(configuration => configuration.Walkover!.LoserScore)
                .GreaterThanOrEqualTo((short)0);
        });

        When(configuration => configuration.Metrics is not null, () =>
        {
            RuleFor(configuration => configuration.Metrics!)
                .Must(metrics => metrics.All(code => !string.IsNullOrWhiteSpace(code)))
                    .WithMessage("Una métrica se nombra por su código.")
                .Must(metrics => metrics.Distinct(StringComparer.Ordinal).Count() == metrics.Count)
                    .WithMessage("Una métrica no puede aparecer dos veces.");
        });
    }
}
