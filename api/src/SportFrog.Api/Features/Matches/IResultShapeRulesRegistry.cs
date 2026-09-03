using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Resolves the result-shape rules that apply for a score mode.
/// </summary>
/// <remarks>
/// The abstraction <see cref="ResultPolicy"/> depends on instead of a mode
/// check. Adding a score mode means registering one more
/// <see cref="IResultShapeRules"/> with the container — <see cref="ResultPolicy"/>
/// does not change to see it.
/// </remarks>
internal interface IResultShapeRulesRegistry
{
    IResultShapeRules For(ScoreMode mode);
}
