using SportFrog.Domain.Matches;

namespace SportFrog.Domain.Rules;

/// <summary>
/// Scoring for a bout decided by a score judges hand down for one
/// performance a side — poomsae's bracket bouts, not a match summed from
/// events or won period by period.
/// </summary>
/// <remarks>
/// A sibling of <see cref="CumulativeMatchOutcomeRules"/> and
/// <see cref="SetsMatchOutcomeRules"/>, not a variant of either: see
/// <see cref="IMatchOutcomeRules"/>'s own remark for why a new mode earns a
/// new implementation instead of inheriting one. The one thing this mode
/// changes from a cumulative score is that a draw is never offered — see
/// <see cref="OptionalOutcomes"/> — because <see cref="JudgedResultShape"/>
/// never lets a tied score reach here in the first place: the judges settle
/// it before a result is recorded, the same way nothing decides a tied set
/// under <see cref="ScoreMode.Sets"/>.
/// </remarks>
public sealed class JudgedMatchOutcomeRules : IMatchOutcomeRules
{
    public ScoreMode Mode => ScoreMode.Judged;

    /// <remarks>
    /// A judged bout is exactly one performance a side —
    /// <see cref="JudgedRulesetShape"/> refuses any other period count — so
    /// summing is the same as reading that one score directly.
    /// </remarks>
    public (int Home, int Away) Consolidate(IReadOnlyList<PeriodScore> periods) =>
        (periods.Sum(period => period.Home), periods.Sum(period => period.Away));

    public IReadOnlyCollection<string> RequiredOutcomes(short periods) => [MatchOutcomes.Win, MatchOutcomes.Loss];

    /// <remarks>
    /// Unlike <see cref="CumulativeMatchOutcomeRules"/>, where whether a
    /// match may end level is left to the competition, a judged bout runs
    /// until the judges produce a winner — offering a draw here would be
    /// offering a result that can never actually be recorded.
    /// </remarks>
    public IReadOnlyCollection<string> OptionalOutcomes() => [];

    public string OutcomeFor(int own, int against) =>
        own > against ? MatchOutcomes.Win : MatchOutcomes.Loss;
}
