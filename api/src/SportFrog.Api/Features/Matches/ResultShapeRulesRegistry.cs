using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Built from every <see cref="IResultShapeRules"/> the container knows
/// about, keyed by the mode each one declares for itself.
/// </summary>
internal sealed class ResultShapeRulesRegistry : IResultShapeRulesRegistry
{
    private readonly IReadOnlyDictionary<ScoreMode, IResultShapeRules> rulesByMode;

    public ResultShapeRulesRegistry(IEnumerable<IResultShapeRules> rules)
    {
        // A duplicate mode registered twice is a wiring mistake and should
        // fail at startup — see SportFrog.Domain.Rules.MatchOutcomeRulesRegistry
        // for the same choice made the same way.
        rulesByMode = rules.ToDictionary(candidate => candidate.Mode);
    }

    public IResultShapeRules For(ScoreMode mode) =>
        rulesByMode.TryGetValue(mode, out var rules)
            ? rules
            : throw new InvalidOperationException(
                $"No hay reglas de forma de resultado registradas para el modo de puntaje '{mode}'.");
}
