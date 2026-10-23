namespace SportFrog.Domain.Scheduling;

/// <summary>
/// Whether the moment a jornada is about to start makes sense to write to
/// the calendar at all.
/// </summary>
/// <remarks>
/// Two different ways a "next jornada" can be nonsense, both checked once
/// before <see cref="CalendarPlacement.Place"/> ever runs rather than after
/// it has already placed something: a start already behind the clock that
/// is about to write it, and a start behind a jornada this same category
/// already has dated. The second is not redundant with the first — an
/// organizer backfilling a calendar for a competition that started weeks ago
/// can land a request past right now and still ask for a round earlier than
/// one this category already played, which is exactly the "jornada" going
/// backwards <see cref="Bracket"/>'s own draw never allows either.
///
/// Pure and clockless on purpose, like the rest of this folder: "now" and
/// "whatever this category already has booked" are facts the caller already
/// had to go fetch from a real clock and a real database, and handing them
/// in keeps the decision itself testable without either.
/// </remarks>
public static class JornadaWindow
{
    /// <summary>Why a requested start was refused, or that it was not.</summary>
    public enum Violation
    {
        None,

        /// <summary>Earlier than right now.</summary>
        InThePast,

        /// <summary>Earlier than a jornada this category already has dated.</summary>
        BeforeAnExistingJornada,
    }

    /// <param name="requestedStart">
    /// The earliest instant this run would place a fixture at — the same
    /// date and start time <see cref="CalendarPlacement.Place"/> is about to
    /// search forward from, combined into one point in time.
    /// </param>
    /// <param name="now">The clock this jornada is being generated against.</param>
    /// <param name="latestScheduled">
    /// When this category's own last dated fixture is, if it has one yet.
    /// Null for a category that has never had a jornada placed — there is
    /// nothing behind it to land before.
    /// </param>
    public static Violation Inspect(DateTimeOffset requestedStart, DateTimeOffset now, DateTimeOffset? latestScheduled)
    {
        if (requestedStart < now)
        {
            return Violation.InThePast;
        }

        if (latestScheduled is { } latest && requestedStart < latest)
        {
            return Violation.BeforeAnExistingJornada;
        }

        return Violation.None;
    }
}
