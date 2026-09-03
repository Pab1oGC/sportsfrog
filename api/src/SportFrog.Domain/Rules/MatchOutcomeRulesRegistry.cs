namespace SportFrog.Domain.Rules;

/// <summary>
/// Built from every <see cref="IMatchOutcomeRules"/> the container knows
/// about, keyed by the mode each one declares for itself.
/// </summary>
/// <remarks>
/// Constructed from <see cref="IEnumerable{T}"/> so the container's own
/// registrations are the only list of modes this ever reads — there is no
/// second list here to keep in sync with <c>Program.cs</c>.
/// </remarks>
public sealed class MatchOutcomeRulesRegistry : IMatchOutcomeRulesRegistry
{
    private readonly IReadOnlyDictionary<ScoreMode, IMatchOutcomeRules> rulesByMode;

    public MatchOutcomeRulesRegistry(IEnumerable<IMatchOutcomeRules> rules)
    {
        // ToDictionary refuses a duplicate key on its own: two
        // implementations registered for the same mode is a wiring mistake,
        // and one should fail loudly at startup rather than have this
        // silently keep whichever happened to be registered last.
        rulesByMode = rules.ToDictionary(candidate => candidate.Mode);
    }

    public IMatchOutcomeRules For(ScoreMode mode) =>
        rulesByMode.TryGetValue(mode, out var rules)
            ? rules
            : throw new InvalidOperationException(
                $"No hay reglas de resultado registradas para el modo de puntaje '{mode}'.");
}
