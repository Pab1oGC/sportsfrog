using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// A judged bout is one performance a side, so its ruleset can only ever
/// declare a single period — and a walkover under it is never read against a
/// scoreline, since a judged score is never built from periods won.
/// </summary>
/// <remarks>Sibling of <see cref="CumulativeRulesetShape"/> and <see cref="SetsRulesetShape"/>.</remarks>
internal sealed class JudgedRulesetShape : IRulesetShapeRules
{
    public ScoreMode Mode => ScoreMode.Judged;

    public bool InspectPeriods(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations)
    {
        if (configuration.Periods.Count == 1)
        {
            return true;
        }

        violations.Add(new RulesetViolation(
            "Config.Periods.Count",
            $"{sport.Name} se decide en una sola actuación por lado, así que la cantidad de " +
            $"{configuration.Periods.Label}s tiene que ser 1."));

        return false;
    }

    /// <remarks>
    /// Same reasoning as <see cref="CumulativeRulesetShape.InspectWalkover"/>:
    /// a judged score is never read against a scoreline the way a set is, so
    /// any pair of numbers the competition wants to record for a walkover is
    /// accepted.
    /// </remarks>
    public void InspectWalkover(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations)
    {
    }
}
