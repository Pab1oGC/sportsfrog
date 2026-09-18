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

    /// <summary>
    /// Whether a live score is a concept this score mode has at all.
    /// </summary>
    /// <remarks>
    /// Only true for <see cref="ScoreMode.Cumulative"/>. <see cref="Compute"/>
    /// only ever tallies events whose metric affects the score, and under
    /// <see cref="ScoreMode.Sets"/> none of them do — so it always returns
    /// zero for a sets-mode match, indistinguishable from a match that is
    /// genuinely level so far. A caller has to ask this first: a "live"
    /// score frozen at 0-0 for the length of the match is not a missing
    /// number, it looks like a real one.
    /// </remarks>
    public static bool AppliesTo(ScoreMode mode) => mode == ScoreMode.Cumulative;

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
    /// hand — every configured period, whether or not anything was recorded
    /// for it.
    /// </summary>
    /// <remarks>
    /// Sound for a cumulative-score sport because every configured period is
    /// played out in full regardless of what either side has scored — a
    /// football match plays both halves at 5-0 exactly as it would at 0-0.
    /// Most metrics under <see cref="ScoreMode.Sets"/> still affect nothing
    /// (a volleyball point is a statistic, never summed into the match), but
    /// where one does — a taekwondo kyorugi point or gam-jeom — a bout that
    /// is already decided does not go on to a period nobody fought, which is
    /// exactly what this method cannot tell from "nothing recorded" alone.
    /// <see cref="ComputePlayedPeriods"/> is the one sound for that case.
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

    /// <summary>
    /// A match's periods, tallied from its events, up to the last one
    /// anything was actually recorded for — the counterpart to
    /// <see cref="ComputePeriods"/> for a sport where the periods themselves
    /// decide the match (<see cref="ScoreMode.Sets"/>) and stop the moment
    /// one side has taken enough of them.
    /// </summary>
    /// <remarks>
    /// A taekwondo kyorugi bout won in two asaltos never fights a third —
    /// nobody records a point or a gam-jeom for it, and <see cref="ComputePeriods"/>
    /// would report that silence as a 0-0 tie rather than as "not reached",
    /// which <see cref="SportFrog.Api.Features.Matches.SetsResultShape"/>
    /// then correctly refuses as an impossible period. Reading "the highest
    /// period number with any event" as "how many were actually fought"
    /// avoids that: a period genuinely fought to a scoreless draw is
    /// indistinguishable from one never reached either way, but that
    /// ambiguity is not new here — a scorer typing the result in by hand
    /// runs into the exact same wall, since <c>SetsResultShape</c> refuses a
    /// tied period regardless of where the numbers came from.
    ///
    /// Periods with no event at all — none recorded, not even a scoreless
    /// one — are not reported, which is different from
    /// <see cref="ComputePeriods"/>, and deliberately so: a cumulative
    /// sport's unplayed period is a data gap, a sets-mode sport's is the
    /// bout being over.
    /// </remarks>
    public static IReadOnlyList<PeriodScore> ComputePlayedPeriods(
        IEnumerable<ScoringEvent> events,
        int maxPeriodCount,
        Guid homeTeamId,
        Guid awayTeamId)
    {
        var byPeriod = events.ToLookup(recorded => Clamp(recorded.PeriodNumber, maxPeriodCount));
        var playedCount = byPeriod.Count == 0 ? 0 : byPeriod.Max(group => group.Key);
        var periods = new List<PeriodScore>(playedCount);

        for (short period = 1; period <= playedCount; period++)
        {
            var totals = Compute(byPeriod[period], homeTeamId, awayTeamId);
            periods.Add(new PeriodScore { Period = period, Home = totals.Home, Away = totals.Away });
        }

        return periods;
    }

    private static short Clamp(short? periodNumber, int periodCount) =>
        periodNumber is { } number && number >= 1 && number <= periodCount ? number : (short)1;
}
