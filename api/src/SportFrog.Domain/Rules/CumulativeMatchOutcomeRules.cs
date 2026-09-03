using SportFrog.Domain.Matches;

namespace SportFrog.Domain.Rules;

/// <summary>
/// Scoring for a match settled by total score — goals, points, runs,
/// whatever the sport counts — added up across every period.
/// </summary>
/// <remarks>
/// Moved out of <see cref="ScoreConsolidation"/> and <see cref="MatchOutcomes"/>
/// as-is: this is the same arithmetic those two used to run inline behind a
/// mode check, now reachable on its own.
/// </remarks>
public sealed class CumulativeMatchOutcomeRules : IMatchOutcomeRules
{
    public ScoreMode Mode => ScoreMode.Cumulative;

    public (int Home, int Away) Consolidate(IReadOnlyList<PeriodScore> periods) =>
        (periods.Sum(period => period.Home), periods.Sum(period => period.Away));

    public IReadOnlyCollection<string> RequiredOutcomes(short periods) => [MatchOutcomes.Win, MatchOutcomes.Loss];

    /// <remarks>
    /// Whether a cumulative match can end level is a competition choice, not
    /// a sport one: basketball under FIBA plays overtime until someone wins,
    /// a municipal league stops at the final buzzer. Offering the draw here
    /// as optional lets a ruleset answer either way.
    /// </remarks>
    public IReadOnlyCollection<string> OptionalOutcomes() => [MatchOutcomes.Draw];

    public string OutcomeFor(int own, int against) =>
        own > against ? MatchOutcomes.Win
        : own == against ? MatchOutcomes.Draw
        : MatchOutcomes.Loss;
}
