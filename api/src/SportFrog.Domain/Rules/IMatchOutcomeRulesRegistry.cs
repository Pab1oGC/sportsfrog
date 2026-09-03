namespace SportFrog.Domain.Rules;

/// <summary>
/// Resolves the match-outcome rules that apply for a score mode.
/// </summary>
/// <remarks>
/// The abstraction a caller depends on instead of a mode check. Adding a
/// score mode means registering one more <see cref="IMatchOutcomeRules"/>
/// with the container — nothing that already resolves through this
/// interface has to change to see it.
/// </remarks>
public interface IMatchOutcomeRulesRegistry
{
    IMatchOutcomeRules For(ScoreMode mode);
}
