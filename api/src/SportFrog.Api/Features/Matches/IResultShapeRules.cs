using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Whether periods that are individually well-formed add up to a result this
/// score mode could have produced.
/// </summary>
/// <remarks>
/// Reached only once <see cref="ResultPolicy"/> has confirmed the periods
/// form an actual sequence — numbered once each, from one, with no negative
/// score. What is left to judge is mode-specific: a cumulative match plays
/// every configured period, a match in sets stops the moment one side has
/// taken enough of them — which is why this is a second, narrower interface
/// rather than a member added to <c>IMatchOutcomeRules</c>: pricing a result
/// and judging whether one could have happened are different questions, and
/// a mode should not have to answer both to answer either.
/// </remarks>
internal interface IResultShapeRules
{
    void Inspect(MatchRules rules, IReadOnlyList<PeriodScore> periods, List<ResultViolation> violations);
}
