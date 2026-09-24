namespace SportFrog.Domain.Rules;

/// <summary>
/// Which minutes of the match clock belong to which period.
/// </summary>
/// <remarks>
/// For a sport whose clock runs on across periods — football, futsal,
/// basketball — an event's minute is the match's own: the second half of a
/// 45-minute football match starts at 46, not at 1. That is how it is read out
/// on television and written on a match sheet, and it is what makes 48 in the
/// first half mean "45+3" while 48 in the second means the third minute after
/// the restart. The period an event is filed under says which.
///
/// A period therefore owns the minutes from just after the previous one ended
/// to its own last minute plus the stoppage time it may add
/// (<see cref="PeriodRules.MaxExtraMinutes"/>). Nothing outside that belongs to
/// it: 99 is not a minute the first half can reach, however long the referee
/// adds.
///
/// Pure, and given the ruleset rather than a match, so that the server's check
/// and the front-end's hint are the same three lines rather than two ideas of
/// what a legal minute is.
/// </remarks>
public static class PeriodClock
{
    /// <summary>
    /// The stoppage time allowed when a reglamento does not say. Ten is what
    /// is usually added; twenty is the extreme case — this is the ceiling, so
    /// it takes the latter.
    /// </summary>
    public const short DefaultMaxExtraMinutes = 20;

    /// <summary>
    /// The first and last minute an event may carry when it is filed under
    /// this period, or null where the question has no answer: a period that
    /// does not run on a clock, or one the match does not have.
    /// </summary>
    /// <remarks>
    /// The first period may start at zero, which the schema has always
    /// allowed and which an event logged at the whistle sits on. Every later
    /// one starts one past the last regular minute of the period before —
    /// stoppage time of the earlier period is not the later one's to claim.
    /// </remarks>
    public static (int From, int To)? MinuteWindow(PeriodRules periods, int period)
    {
        if (periods.Minutes is not { } length || period < 1 || period > periods.Count)
        {
            return null;
        }

        var extra = periods.MaxExtraMinutes ?? DefaultMaxExtraMinutes;

        var from = period == 1 ? 0 : (period - 1) * length + 1;
        var to = period * length + extra;

        return (from, to);
    }

    /// <summary>
    /// The latest minute anything in the match can carry, for an event that
    /// was not filed under any period: the last period's end plus its
    /// stoppage time.
    /// </summary>
    public static int? LastMinute(PeriodRules periods) =>
        MinuteWindow(periods, periods.Count)?.To;
}
