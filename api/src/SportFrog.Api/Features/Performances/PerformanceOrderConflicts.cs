namespace SportFrog.Api.Features.Performances;

/// <summary>
/// One performance's slot in a running order, as it would stand after a batch
/// of moves is applied — whether this particular performance is one being
/// moved, or merely one already sitting somewhere the batch might now reach.
/// </summary>
internal sealed record OrderSlot(
    Guid PerformanceId,
    string TeamName,
    Guid? VenueSpaceId,
    DateOnly? ScheduledOn,
    short? OrderNumber);

/// <summary>
/// Whether a batch of running-order moves lands every one of them on a free
/// turn, all at once.
/// </summary>
/// <remarks>
/// The running-order sibling of <see cref="Matches.BulkRescheduleConflicts"/>,
/// for the same reason: swapping two teams' turns one request at a time
/// always tries to put the first where the second still stands, and gets
/// refused for it. Comparing the *final* arrangement as a whole is what makes
/// a swap succeed in one step instead of failing on the order it happens to
/// be attempted in.
///
/// Unlike matches, there is no team-collision half here — see
/// <see cref="PerformancePolicy"/>'s remarks on why a turn number carries no
/// synchronized real time to compare a team's other turns against. The only
/// thing that can actually collide is the slot itself: one mat, one day, one
/// turn, claimed twice.
/// </remarks>
internal static class PerformanceOrderConflicts
{
    /// <summary>
    /// One violation per move that would collide with something, keyed by
    /// index into the original request's <c>Moves[i]</c>.
    /// </summary>
    public static IReadOnlyDictionary<int, List<string>> Find(
        IReadOnlyList<(int Index, OrderSlot Slot)> moved, IReadOnlyList<OrderSlot> others)
    {
        var violations = new Dictionary<int, List<string>>();

        void Add(int index, string message)
        {
            if (!violations.TryGetValue(index, out var messages))
            {
                violations[index] = messages = [];
            }

            messages.Add(message);
        }

        var all = moved.Select(entry => entry.Slot).Concat(others).ToList();

        for (var i = 0; i < moved.Count; i++)
        {
            var (index, mine) = moved[i];

            if (mine.VenueSpaceId is null || mine.ScheduledOn is null || mine.OrderNumber is null)
            {
                continue;
            }

            foreach (var other in all)
            {
                if (other.PerformanceId == mine.PerformanceId
                    || other.VenueSpaceId != mine.VenueSpaceId
                    || other.ScheduledOn != mine.ScheduledOn
                    || other.OrderNumber != mine.OrderNumber)
                {
                    continue;
                }

                Add(index, $"Ese turno quedaría también con {other.TeamName}.");
            }
        }

        return violations;
    }
}
