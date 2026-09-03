using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Built from every <see cref="IRulesetShapeRules"/> the container knows
/// about, keyed by the mode each one declares for itself.
/// </summary>
internal sealed class RulesetShapeRulesRegistry : IRulesetShapeRulesRegistry
{
    private readonly IReadOnlyDictionary<ScoreMode, IRulesetShapeRules> rulesByMode;

    public RulesetShapeRulesRegistry(IEnumerable<IRulesetShapeRules> rules)
    {
        // A duplicate mode registered twice is a wiring mistake and should
        // fail at startup — see SportFrog.Domain.Rules.MatchOutcomeRulesRegistry
        // for the same choice made the same way.
        rulesByMode = rules.ToDictionary(candidate => candidate.Mode);
    }

    public IRulesetShapeRules For(ScoreMode mode) =>
        rulesByMode.TryGetValue(mode, out var rules)
            ? rules
            : throw new InvalidOperationException(
                $"No hay reglas de forma de reglamento registradas para el modo de puntaje '{mode}'.");
}
