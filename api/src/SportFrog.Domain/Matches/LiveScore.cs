namespace SportFrog.Domain.Matches;

/// <summary>
/// One recorded event that moves the score, stripped to what tallying it
/// needs.
/// </summary>
/// <param name="TeamId">
/// The team the roster entry that recorded it plays for — not necessarily
/// who the points count for; see <see cref="CountsForOpponent"/>.
/// </param>
/// <param name="PeriodNumber">
/// Which period this happened in, where that matters — only
/// <see cref="LiveScore.ComputePeriods"/> reads it. Optional and defaulted so
/// every existing caller that has no use for it keeps compiling unchanged.
/// </param>
public readonly record struct ScoringEvent(
    Guid TeamId,
    int ScorePoints,
    bool CountsForOpponent,
    int Quantity,
    short? PeriodNumber = null);

/// <summary>
/// The score read from events recorded so far, while a match is still being
/// played and before anyone has written down its result.
/// </summary>
/// <remarks>
/// Deliberately not the same thing <see cref="ScoreConsolidation"/> produces.
/// That one turns a finished match's periods into its final score and is the
/// only number a standings table ever reads — a scorer can misclick a metric
/// mid-game and correct it before anyone writes the result down, so nothing
/// here is ever treated as authoritative. This exists only so a match in
/// progress does not have to show "vs" to somebody watching the page while
/// it is actually 2-1: a running tally of what has been recorded, offered
/// alongside the real thing and replaced by it the moment the match finishes.
///
/// Only events whose metric has <c>AffectsScore</c> true are ever passed in
/// here, which for <see cref="ScoreMode.Sets"/> sports is none of them — a
/// live score naturally has nothing to compute for those, and callers see
/// zero events rather than a wrong number.
/// </remarks>
public static class LiveScore
{
    public readonly record struct Totals(int Home, int Away);

    public static Totals Compute(
        IEnumerable<ScoringEvent> events,
        Guid homeTeamId,
        Guid awayTeamId)
    {
        var home = 0;
        var away = 0;

        foreach (var recorded in events)
        {
            var points = recorded.ScorePoints * recorded.Quantity;

            // An own goal is credited to the roster entry's team but scored
            // against it — the points belong to whoever that team is playing.
            var scoringTeam = recorded.CountsForOpponent
                ? Opponent(recorded.TeamId, homeTeamId, awayTeamId)
                : recorded.TeamId;

            if (scoringTeam == homeTeamId)
            {
                home += points;
            }
            else if (scoringTeam == awayTeamId)
            {
                away += points;
            }

            // A team that is neither side of this match — data that should
            // never exist — is silently not counted rather than thrown for,
            // the same tolerance a read endpoint extends everywhere else.
        }

        return new Totals(home, away);
    }

    private static Guid Opponent(Guid teamId, Guid homeTeamId, Guid awayTeamId) =>
        teamId == homeTeamId ? awayTeamId : homeTeamId;

    /// <summary>
    /// A match's periods, tallied from its events instead of typed in by
    /// hand.
    /// </summary>
    /// <remarks>
    /// Only sound for a cumulative-score sport: <see cref="Compute"/> already
    /// only ever sees events whose metric affects the score, which under
    /// <see cref="ScoreMode.Sets"/> is none of them, and a match decided by
    /// sets was never going to be finished this way regardless.
    ///
    /// An event with no period recorded, or one outside the range this sport
    /// actually plays, is folded into the first period rather than dropped.
    /// <see cref="ScoreConsolidation"/> sums every period for a cumulative
    /// sport, so which one a goal lands in changes nothing about the total —
    /// but silently losing it because nobody chose a period from a dropdown
    /// would.
    /// </remarks>
    public static IReadOnlyList<PeriodScore> ComputePeriods(
        IEnumerable<ScoringEvent> events,
        int periodCount,
        Guid homeTeamId,
        Guid awayTeamId)
    {
        var byPeriod = events.ToLookup(recorded => Clamp(recorded.PeriodNumber, periodCount));
        var periods = new List<PeriodScore>(periodCount);

        for (short period = 1; period <= periodCount; period++)
        {
            var totals = Compute(byPeriod[period], homeTeamId, awayTeamId);
            periods.Add(new PeriodScore { Period = period, Home = totals.Home, Away = totals.Away });
        }

        return periods;
    }

    private static short Clamp(short? periodNumber, int periodCount) =>
        periodNumber is { } number && number >= 1 && number <= periodCount ? number : (short)1;
}
