
namespace SportFrog.Domain.Scheduling;

/// <summary>A fixture waiting for a date, and the two teams that cannot be in two places at once.</summary>
public sealed record PendingFixture(Guid MatchId, Guid HomeTeamId, Guid AwayTeamId);

/// <summary>
/// A pitch, occupied from one moment to another.
/// </summary>
/// <param name="EndsAt">
/// When the pitch is free again — the match itself plus whatever buffer the
/// organizer leaves before the next one. Always strictly after <paramref
/// name="At"/>: callers building one from a resolved duration and a
/// non-negative buffer get this for free, and <see cref="CalendarPlacement"/>
/// depends on it to guarantee its own search always moves forward.
/// </param>
public sealed record Booking(Guid VenueSpaceId, DateTimeOffset At, DateTimeOffset EndsAt);

/// <summary>Where one fixture ended up, and until when it holds the pitch.</summary>
public sealed record Placement(Guid MatchId, Guid VenueSpaceId, DateTimeOffset At, DateTimeOffset EndsAt);

/// <summary>
/// Puts drawn fixtures onto the pitches an organization has said this
/// competition may use.
/// </summary>
/// <remarks>
/// Separate from the draw because they answer different questions and change
/// for different reasons: the draw is who plays whom, this is when the ground
/// is free. Kept apart, a calendar can be redrawn without renegotiating every
/// booking, and a venue can be lost without redrawing anything.
///
/// The rule that does the work is not slot arithmetic — it is that a team
/// plays at most once a day. Without it the first pitch of the first Saturday
/// swallows the whole first round and half of it is the same four teams.
///
/// No database and no clock of its own, and no notion of a category or a
/// ruleset either: fixtures, a duration, a set of pitches and a starting date
/// go in, placements come out. One call is always one jornada — a single
/// round of a single category — so every fixture in <paramref name="pending"/>
/// shares the same duration; what varies is <paramref name="taken"/>, which
/// can carry bookings from other jornadas or other competitions sharing the
/// same pitches, each on its own duration.
///
/// Deliberately ignorant of which hours a pitch is actually free: reserving
/// one is a real-world arrangement an organizer settles with whoever owns it
/// before any of this runs, not something this software could know on its
/// own. Modelling "open Saturdays, eight to two" here would only pretend
/// otherwise, and would still be wrong the day a venue makes an exception.
/// What this needs is which pitches are in play at all — the composing
/// endpoint's own settings answer that — and what time a given run should
/// start from, which its request does.
/// </remarks>
public static class CalendarPlacement
{
    /// <summary>How far ahead to look before giving up. Two years of weekends is not a calendar.</summary>
    private const int MaximumDaysSearched = 730;

    /// <summary>
    /// Places as many fixtures as fit.
    /// </summary>
    /// <param name="fixtureDurationMinutes">
    /// How long playing one of these matches takes, resolved by the caller
    /// from the jornada's own ruleset (or its manual fallback) before this is
    /// ever called — this function does not know what a ruleset is.
    /// </param>
    /// <param name="bufferMinutes">
    /// Room left on the pitch after one match before the next may start.
    /// Folded into every <see cref="Booking"/> this produces, so a later call
    /// checking against them never has to add it again.
    /// </param>
    /// <param name="spaceIds">
    /// The pitches this competition may use. Which ones, nothing more — see
    /// the class remarks for why there is no notion of hours to go with them.
    /// </param>
    /// <param name="taken">
    /// Bookings that already exist, including fixtures somebody placed by
    /// hand or jornadas placed by an earlier call. Respected rather than
    /// overwritten: the database would refuse the clash anyway, and an
    /// organizer who agreed a time with a venue should not have it moved by a
    /// later run.
    /// </param>
    /// <param name="engaged">
    /// Teams that already have a match on a given day, from fixtures placed
    /// before this ran. Counted for the same reason the bookings are: a
    /// fixture somebody set by hand occupies its pitch <em>and</em> its two
    /// teams, and a placement that only saw the pitch would schedule one of
    /// them twice on the same afternoon.
    /// </param>
    /// <param name="startTime">
    /// The first moment a match may start, every day this run reaches —
    /// there being no per-pitch opening hour left to fall back on once this
    /// anchors day one. Reused unchanged for as many days as the run needs.
    /// </param>
    /// <returns>
    /// What was placed. Anything left over is the caller's to report — a
    /// jornada with more fixtures than hours is a real situation, and
    /// silently dropping the remainder would hide it.
    /// </returns>
    public static IReadOnlyList<Placement> Place(
        IReadOnlyList<PendingFixture> pending,
        short fixtureDurationMinutes,
        short bufferMinutes,
        IReadOnlyList<Guid> spaceIds,
        IReadOnlyList<Booking> taken,
        IReadOnlyList<(Guid Team, DateOnly Day)> engaged,
        DateOnly from,
        TimeSpan offset,
        TimeOnly startTime)
    {
        if (spaceIds.Count == 0 || fixtureDurationMinutes <= 0 || pending.Count == 0)
        {
            return [];
        }

        var buffer = Math.Max((short)0, bufferMinutes);

        var booked = new Dictionary<Guid, List<Booking>>();
        foreach (var booking in taken)
        {
            (booked.TryGetValue(booking.VenueSpaceId, out var list)
                ? list
                : booked[booking.VenueSpaceId] = []).Add(booking);
        }

        var placements = new List<Placement>();

        // A team plays once a day, seeded with the days it is already
        // committed to and grown as this run commits it to more.
        var busy = engaged.ToHashSet();

        foreach (var fixture in pending)
        {
            if (Fit(fixture, spaceIds, fixtureDurationMinutes, buffer, booked, busy, from, offset, startTime)
                is not { } placement)
            {
                // Out of room within the horizon — every team involved is
                // already committed on every day the search reached. The
                // rest are no better off — they are later in the same queue
                // — so the search stops rather than repeating it for each.
                break;
            }

            placements.Add(placement);
            (booked.TryGetValue(placement.VenueSpaceId, out var list)
                ? list
                : booked[placement.VenueSpaceId] = []).Add(
                    new Booking(placement.VenueSpaceId, placement.At, placement.EndsAt));
            busy.Add((fixture.HomeTeamId, DateOnly.FromDateTime(placement.At.Date)));
            busy.Add((fixture.AwayTeamId, DateOnly.FromDateTime(placement.At.Date)));
        }

        return placements;
    }

    /// <summary>
    /// The earliest this fixture can be placed across every pitch, searching
    /// forward day by day until it finds one where neither team is already
    /// committed.
    /// </summary>
    /// <remarks>
    /// Once such a day is found there is always a slot to return — nothing
    /// bounds how late one may start (see the class remarks) — so the only
    /// reason this ever moves to the next day is the one-match-a-day rule,
    /// never a pitch running out of room. Tries every pitch that day and
    /// keeps the earliest result rather than the first one with room — a
    /// second pitch sitting empty while the first fills up one match at a
    /// time is exactly the waste this guards against.
    /// </remarks>
    private static Placement? Fit(
        PendingFixture fixture,
        IReadOnlyList<Guid> spaceIds,
        short durationMinutes,
        short bufferMinutes,
        Dictionary<Guid, List<Booking>> booked,
        HashSet<(Guid Team, DateOnly Day)> busy,
        DateOnly from,
        TimeSpan offset,
        TimeOnly startTime)
    {
        for (var day = 0; day < MaximumDaysSearched; day++)
        {
            var date = from.AddDays(day);

            if (busy.Contains((fixture.HomeTeamId, date))
                || busy.Contains((fixture.AwayTeamId, date)))
            {
                continue;
            }

            Placement? best = null;

            foreach (var spaceId in spaceIds)
            {
                var candidate = EarliestSlot(fixture, spaceId, date, startTime, durationMinutes, bufferMinutes, booked, offset);

                if (best is null || candidate.At < best.At)
                {
                    best = candidate;
                }
            }

            // spaceIds is never empty here — Place already returned early
            // otherwise — so the loop above always assigns best at least once.
            return best;
        }

        return null;
    }

    /// <summary>
    /// The earliest this fixture fits on one pitch on one day, jumping past
    /// whatever already blocks it instead of stepping through a fixed grid.
    /// </summary>
    /// <remarks>
    /// A fixed step only works when everything booked shares one duration.
    /// Once a pitch can carry bookings from jornadas of different lengths, a
    /// candidate that lands between two grid points of an earlier booking can
    /// still genuinely overlap it — this checks real intervals instead.
    /// </remarks>
    private static Placement EarliestSlot(
        PendingFixture fixture,
        Guid spaceId,
        DateOnly date,
        TimeOnly startTime,
        short durationMinutes,
        short bufferMinutes,
        Dictionary<Guid, List<Booking>> booked,
        TimeSpan offset)
    {
        var at = new DateTimeOffset(date.ToDateTime(startTime), offset);
        booked.TryGetValue(spaceId, out var existing);

        while (true)
        {
            var gameEnds = at.AddMinutes(durationMinutes);
            var occupiedUntil = gameEnds.AddMinutes(bufferMinutes);

            // The furthest-reaching conflict, not just any one: jumping past
            // the first overlap found could still land inside a second one
            // that overlapped both, costing an extra pass through a run of
            // back-to-back bookings for nothing.
            var blocker = existing?
                .Where(booking => at < booking.EndsAt && booking.At < occupiedUntil)
                .OrderByDescending(booking => booking.EndsAt)
                .FirstOrDefault();

            if (blocker is null)
            {
                return new Placement(fixture.MatchId, spaceId, at, occupiedUntil);
            }

            // Booking.EndsAt is guaranteed strictly after Booking.At (see its
            // own remark), and blocker.EndsAt > at is exactly the condition
            // that found this conflict — so this always moves forward.
            at = blocker.EndsAt;
        }
    }
}
