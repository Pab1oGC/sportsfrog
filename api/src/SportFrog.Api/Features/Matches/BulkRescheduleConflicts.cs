namespace SportFrog.Api.Features.Matches;

/// <summary>
/// One fixture's place on the calendar, as it would stand after a batch of
/// moves is applied — whether this particular fixture is one being moved, or
/// merely one already sitting somewhere the batch might now reach.
/// </summary>
/// <param name="AthleteIds">
/// Everyone currently on either team's roster, home and away combined. Not
/// just the two team ids: the same person can be entered under a second,
/// unrelated <c>Team</c> row in another category or competition, and
/// comparing team ids alone would never see that it is the same person
/// double-booked. See <see cref="BulkRescheduleConflicts"/>.
/// </param>
internal sealed record ScheduledSlot(
    Guid MatchId,

    /// <summary>
    /// Null for a knockout slot drawn in full whose side is still "whoever
    /// wins another match" — nobody to collide over yet.
    /// </summary>
    Guid? HomeTeamId,
    string HomeTeamName,
    Guid? AwayTeamId,
    string AwayTeamName,
    Guid? VenueSpaceId,
    DateTimeOffset? ScheduledAt,
    IReadOnlyCollection<Guid> AthleteIds);

/// <summary>
/// Whether a batch of fixture moves lands every one of them somewhere free,
/// all at once.
/// </summary>
/// <remarks>
/// <see cref="FixturePolicy"/> answers this same question for one fixture
/// against the calendar as it stands today. Moving several at once needs a
/// different question: not "is this free right now", but "once every move in
/// this batch has happened, is anything left sharing a ground or a team with
/// anything else" — which is why a swap (A takes B's slot, B takes A's) never
/// looks like a conflict here, even though asking one at a time in the wrong
/// order always would.
///
/// Pure and synchronous on purpose: every fixture that could possibly matter
/// — every one being moved, and every other live fixture that shares a
/// ground, a team, or a roster athlete with one of them — is loaded once, up
/// front, and compared in memory. Nothing here queries anything, which is
/// what makes it cheap to call before touching the database and cheap to
/// test without one.
/// </remarks>
internal static class BulkRescheduleConflicts
{
    /// <summary>
    /// One violation per move that would collide with something, keyed by
    /// index into the original request — the caller's <c>Moves[i]</c>, not a
    /// fixture id, because a request is validated before anything is loaded
    /// far enough to say which id belongs to which slot.
    /// </summary>
    public static IReadOnlyDictionary<int, List<string>> Find(
        IReadOnlyList<(int Index, ScheduledSlot Slot)> moved, IReadOnlyList<ScheduledSlot> others)
    {
        var violations = new Dictionary<int, List<string>>();

        static bool Shares(Guid? a, Guid? b) => a is not null && a == b;

        void Add(int index, string message)
        {
            if (!violations.TryGetValue(index, out var messages))
            {
                violations[index] = messages = [];
            }

            messages.Add(message);
        }

        // Every slot that could conflict, moved ones included: two moves in
        // the same batch can collide with each other just as easily as one
        // of them can collide with a fixture nobody touched.
        var all = moved.Select(entry => entry.Slot).Concat(others).ToList();

        for (var i = 0; i < moved.Count; i++)
        {
            var (index, mine) = moved[i];

            if (mine.ScheduledAt is null)
            {
                continue;
            }

            foreach (var other in all)
            {
                if (other.MatchId == mine.MatchId || other.ScheduledAt != mine.ScheduledAt)
                {
                    continue;
                }

                if (mine.VenueSpaceId is not null && other.VenueSpaceId == mine.VenueSpaceId)
                {
                    Add(index, $"Esa cancha quedaría con {other.HomeTeamName} vs {other.AwayTeamName} a la misma hora.");
                }

                // Guarded by "not null" on the side compared, not just
                // "equal": two knockout slots that both have no team yet
                // would otherwise compare null == null and report a team
                // playing itself twice, when neither names one at all.
                if (Shares(other.HomeTeamId, mine.HomeTeamId) || Shares(other.AwayTeamId, mine.HomeTeamId)
                    || Shares(other.HomeTeamId, mine.AwayTeamId) || Shares(other.AwayTeamId, mine.AwayTeamId))
                {
                    Add(index, $"Un equipo quedaría jugando dos partidos a la vez: contra {other.HomeTeamName} vs {other.AwayTeamName}.");
                }
                else if (mine.AthleteIds.Count > 0 && other.AthleteIds.Any(mine.AthleteIds.Contains))
                {
                    // Different teams, but the same person stands behind one
                    // of each — a second registration, not a second look at
                    // the same fixture.
                    Add(index,
                        "Uno de sus deportistas quedaría jugando dos partidos a la vez, en otra " +
                        $"inscripción: contra {other.HomeTeamName} vs {other.AwayTeamName}.");
                }
            }
        }

        return violations;
    }
}
