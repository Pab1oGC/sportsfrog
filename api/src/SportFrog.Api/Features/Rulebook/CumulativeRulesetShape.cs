using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// A cumulative ruleset has no shape constraint beyond what
/// <c>InspectPoints</c> and <c>InspectMetrics</c> already check.
/// </summary>
/// <remarks>
/// Any period count is playable, and a walkover awarded under a cumulative
/// score is not read against a scoreline the way a set is — any pair of
/// numbers the competition wants to record is accepted.
/// </remarks>
internal sealed class CumulativeRulesetShape : IRulesetShapeRules
{
    public ScoreMode Mode => ScoreMode.Cumulative;

    public bool InspectPeriods(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations) =>
        true;

    public void InspectWalkover(Sport sport, RulesetConfiguration configuration, List<RulesetViolation> violations)
    {
    }
}
