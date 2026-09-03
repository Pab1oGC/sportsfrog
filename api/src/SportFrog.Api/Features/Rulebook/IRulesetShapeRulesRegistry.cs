using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Resolves the ruleset-shape rules that apply for a score mode.
/// </summary>
/// <remarks>
/// The abstraction <see cref="RulesetPolicy"/> depends on instead of a mode
/// check. Adding a score mode means registering one more
/// <see cref="IRulesetShapeRules"/> with the container —
/// <see cref="RulesetPolicy"/> does not change to see it.
/// </remarks>
internal interface IRulesetShapeRulesRegistry
{
    IRulesetShapeRules For(ScoreMode mode);
}
