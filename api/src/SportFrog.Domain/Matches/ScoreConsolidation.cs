using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Matches;

/// <summary>
/// Turns the score of each period into the score of the match.
/// </summary>
/// <remarks>
/// The whole reason this is not a sum. Under
/// <see cref="ScoreMode.Cumulative"/> the match score is the total of what was
/// scored: a football match of 1-0 and 2-1 finished 3-1. Under
/// <see cref="ScoreMode.Sets"/> the periods decide themselves and the match
/// counts how many each side took: a volleyball match of 25-20, 22-25, 25-18
/// finished 2-1, and the 72 points scored are not the result of anything.
///
/// Getting this backwards is the kind of mistake that produces a standings
/// table nobody can explain, so it lives in one place with no database
/// underneath it — the sport goes in, the totals come out, and it can be
/// reasoned about on its own.
///
/// The arithmetic itself now lives in <see cref="IMatchOutcomeRules"/> and
/// its two implementations, resolved through
/// <see cref="IMatchOutcomeRulesRegistry"/> — this stays as the entry point
/// every existing caller already uses, so nothing that calls it today has to
/// change to keep working.
/// </remarks>
public static class ScoreConsolidation
{
    // A registry of its own rather than a mode check: this facade uses the
    // same resolution a caller that depends on IMatchOutcomeRulesRegistry
    // through the container would get, so a mode registered there is a mode
    // this sees too, without a line here changing.
    private static readonly IMatchOutcomeRulesRegistry Registry =
        new MatchOutcomeRulesRegistry([new CumulativeMatchOutcomeRules(), new SetsMatchOutcomeRules()]);

    /// <summary>
    /// The match score, read the way the sport reads it.
    /// </summary>
    public static (int Home, int Away) Consolidate(
        ScoreMode mode,
        IReadOnlyList<PeriodScore> periods) =>
        Registry.For(mode).Consolidate(periods);

    /// <summary>
    /// How many periods a side must take to win, where taking periods is how
    /// the match is won at all.
    /// </summary>
    /// <remarks>
    /// Best of five is won at three. The configured count is validated odd
    /// when the ruleset is written, so there is always a deciding one.
    /// </remarks>
    public static int PeriodsToWin(short configuredPeriods) => (configuredPeriods + 1) / 2;
}
