namespace SportFrog.Domain.School;

/// <summary>One weekly slot a course meets in — a day of the week and the hours it runs.</summary>
/// <remarks>
/// A course with two slots on the same day (a morning and an evening group,
/// say) is not rejected here — nothing about turning slots into dates needs
/// to know that is unusual. Whether a course may repeat a day at all is a
/// question for whoever edits the schedule, not for the planner that reads
/// it afterwards.
/// </remarks>
public sealed record WeeklySlot(DayOfWeek Day, TimeOnly StartsAt, TimeOnly EndsAt);

/// <summary>One date a course meets on, with the hours copied from the slot that produced it.</summary>
/// <remarks>
/// Copied rather than looked up again for the same reason a <see
/// cref="Documents.CredentialSnapshot"/> copies what it prints: a schedule
/// edited after this session exists must not reach back and move a class
/// that already happened, or one already told to its students.
/// </remarks>
public sealed record PlannedSession(DateOnly Date, TimeOnly StartsAt, TimeOnly EndsAt);

/// <summary>
/// Turns a course's weekly schedule into the actual dates it meets on.
/// </summary>
/// <remarks>
/// A schedule says "every Tuesday" — true forever, and useless on its own:
/// attendance is taken against one afternoon, not against Tuesdays in
/// general, and a single cancelled class cannot be recorded without a row of
/// its own to mark as cancelled. This is what turns the standing rule into
/// the rows that need it, the same relationship a drawn fixture has to the
/// pitch it is later placed on (<see cref="CalendarPlacement"/>) — who meets
/// whom and when a ground is free are different questions, and so are a
/// recurring slot and the date it lands on.
///
/// No notion of a course's own calendar beyond the range it is asked about:
/// callers decide how far to materialize at once — a month at a time is the
/// expected use — and this does not fail or truncate a wider range, so that
/// restraint is a choice made above it, not a limit enforced here.
/// </remarks>
public static class SessionPlanner
{
    /// <summary>
    /// Every date between <paramref name="from"/> and <paramref name="to"/>,
    /// both included, that lands on one of <paramref name="slots"/>' days.
    /// </summary>
    /// <returns>
    /// Ordered by date, and within a date by start time — so a course with
    /// two sessions in one day always lists the earlier one first,
    /// regardless of which order its schedule rows were given in.
    /// </returns>
    public static IReadOnlyList<PlannedSession> Plan(
        IReadOnlyList<WeeklySlot> slots,
        DateOnly from,
        DateOnly to)
    {
        if (slots.Count == 0 || to < from)
        {
            return [];
        }

        var byDay = slots.ToLookup(slot => slot.Day);
        var sessions = new List<PlannedSession>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var slot in byDay[date.DayOfWeek].OrderBy(slot => slot.StartsAt))
            {
                sessions.Add(new PlannedSession(date, slot.StartsAt, slot.EndsAt));
            }
        }

        return sessions;
    }
}
