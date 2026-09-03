using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// A sport played in sets needs a deciding one, and a walkover has to be
/// recorded with a scoreline the match could actually have finished on.
/// </summary>
/// <remarks>Moved out of <see cref="RulesetPolicy"/> as-is.</remarks>
internal sealed class SetsRulesetShape : IRulesetShapeRules
{
    public ScoreMode Mode => ScoreMode.Sets;

    public bool InspectPeriods(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations)
    {
        if (configuration.Periods.Count % 2 != 0)
        {
            return true;
        }

        violations.Add(new RulesetViolation(
            "Config.Periods.Count",
            $"{sport.Name} se juega por sets, así que la cantidad de sets debe ser impar: " +
            "un número par deja un partido que no se puede ganar."));

        return false;
    }

    public void InspectWalkover(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations)
    {
        if (configuration.Walkover is not { } walkover)
        {
            return;
        }

        // Where the match score is sets won, the score awarded for a walkover
        // has to be a scoreline the match could have finished on.
        var toWin = ScoreConsolidation.PeriodsToWin(configuration.Periods.Count);

        if (walkover.WinnerScore != toWin)
        {
            violations.Add(new RulesetViolation(
                "Config.Walkover.WinnerScore",
                $"Un partido de {sport.Name} se gana en {toWin} sets, así que un walkover se " +
                $"registra con {toWin} y no con {walkover.WinnerScore}."));
        }

        if (walkover.LoserScore >= toWin)
        {
            violations.Add(new RulesetViolation(
                "Config.Walkover.LoserScore",
                $"Al lado que no se presentó no se le puede acreditar {toWin} sets o más."));
        }
    }
}
