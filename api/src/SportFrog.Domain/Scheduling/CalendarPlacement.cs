
namespace SportFrog.Domain.Scheduling;

/// <summary>A fixture waiting for a date, and the two teams that cannot be in two places at once.</summary>
public sealed record PendingFixture(Guid MatchId, int Round, Guid HomeTeamId, Guid AwayTeamId);

/// <summary>A pitch and a moment, already taken.</summary>
public sealed record Booking(Guid VenueSpaceId, DateTimeOffset At);

/// <summary>Where one fixture ended up.</summary>
public sealed record Placement(Guid MatchId, Guid VenueSpaceId, DateTimeOffset At);

/// <summary>
/// Puts drawn fixtures onto the pitches and hours an organization actually
/// has.
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
/// No database and no clock of its own: fixtures, windows and a starting date
/// go in, placements come out.
/// </remarks>
public static class CalendarPlacement
{
    /// <summary>How far ahead to look before giving up. Two years of weekends is not a calendar.</summary>
    private const int MaximumDaysSearched = 730;

    /// <summary>
    /// Places as many fixtures as fit, in round order.
    /// </summary>
    /// <param name="taken">
    /// Bookings that already exist, including fixtures somebody placed by
    /// hand. Respected rather than overwritten: the database would refuse the
    /// clash anyway, and an organizer who agreed a time with a venue should
    /// not have it moved by a redraw.
    /// </param>
    /// <returns>
    /// What was placed. Anything left over is the caller's to report — a
    /// competition with more fixtures than hours is a real situation, and
    /// silently dropping the remainder would hide it.
    /// </returns>
    /// <param name="engaged">
    /// Teams that already have a match on a given day, from fixtures placed
    /// before this ran. Counted for the same reason the bookings are: a
    /// fixture somebody set by hand occupies its pitch <em>and</em> its two
    /// teams, and a placement that only saw the pitch would schedule one of
    /// them twice on the same afternoon.
    /// </param>
    public static IReadOnlyList<Placement> Place(
        IReadOnlyList<PendingFixture> pending,
        ScheduleSettings settings,
        IReadOnlyList<Booking> taken,
        IReadOnlyList<(Guid Team, DateOnly Day)> engaged,
        DateOnly from,
        TimeSpan offset)
    {
        var windows = Windows(settings);

        if (windows.Count == 0 || settings.SlotMinutes <= 0 || pending.Count == 0)
        {
            return [];
        }

        var booked = taken.ToHashSet();
        var placements = new List<Placement>();

        // A team plays once a day, seeded with the days it is already
        // committed to and grown as this run commits it to more.
        var busy = engaged.ToHashSet();

        foreach (var fixture in pending.OrderBy(match => match.Round))
        {
            if (Fit(fixture, windows, settings.SlotMinutes, booked, busy, from, offset)
                is not { } placement)
            {
                // Out of room within the horizon. The rest are no better off
                // — they are later in the same queue — so the search stops
                // rather than repeating it for each.
                break;
            }

            placements.Add(placement);
            booked.Add(new Booking(placement.VenueSpaceId, placement.At));
            busy.Add((fixture.HomeTeamId, DateOnly.FromDateTime(placement.At.Date)));
            busy.Add((fixture.AwayTeamId, DateOnly.FromDateTime(placement.At.Date)));
        }

        return placements;
    }

    /// <summary>
    /// The first slot this fixture can take, searching forward day by day.
    /// </summary>
    private static Placement? Fit(
        PendingFixture fixture,
        IReadOnlyList<Window> windows,
        short slotMinutes,
        HashSet<Booking> booked,
        HashSet<(Guid Team, DateOnly Day)> busy,
        DateOnly from,
        TimeSpan offset)
    {
        for (var day = 0; day < MaximumDaysSearched; day++)
        {
            var date = from.AddDays(day);

            if (busy.Contains((fixture.HomeTeamId, date))
                || busy.Contains((fixture.AwayTeamId, date)))
            {
                continue;
            }

            foreach (var window in windows.Where(candidate => candidate.Covers(date)))
            {
                for (var start = window.From;
                     start <= window.To;
                     start = start.AddMinutes(slotMinutes))
                {
                    var at = new DateTimeOffset(date.ToDateTime(start), offset);

                    if (!booked.Contains(new Booking(window.VenueSpaceId, at)))
                    {
                        return new Placement(fixture.MatchId, window.VenueSpaceId, at);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The usable windows, dropping anything that cannot be read.
    /// </summary>
    /// <remarks>
    /// A window with no times is not an error to throw over — the settings are
    /// a document somebody edits — but it also cannot be scheduled against, so
    /// it is left out and the caller sees fewer fixtures placed than it asked
    /// for.
    /// </remarks>
    private static List<Window> Windows(ScheduleSettings settings) =>
    [
        .. (settings.Spaces ?? [])
            .Select(space => (
                space,
                From: TimeOnly.TryParse(space.From, out var opens) ? opens : (TimeOnly?)null,
                To: TimeOnly.TryParse(space.To, out var closes) ? closes : (TimeOnly?)null))
            .Where(entry => entry.From is not null
                && entry.To is not null
                && entry.From <= entry.To
                && entry.space.VenueSpaceId != Guid.Empty)
            .Select(entry => new Window(
                entry.space.VenueSpaceId,
                entry.From!.Value,
                entry.To!.Value,
                entry.space.Days)),
    ];

    /// <summary>One pitch, on certain days, between certain hours.</summary>
    private sealed record Window(
        Guid VenueSpaceId,
        TimeOnly From,
        TimeOnly To,
        IReadOnlyList<int>? Days)
    {
        /// <summary>
        /// Whether this window is open on a date. No days listed means every
        /// day, which is what a venue with no restriction is.
        /// </summary>
        public bool Covers(DateOnly date) =>
            Days is null || Days.Count == 0 || Days.Contains((int)date.DayOfWeek);
    }
}
