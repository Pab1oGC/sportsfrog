namespace SportFrog.Api.Features.Statistics;

/// <summary>One player's total of one metric, before anything is ranked.</summary>
internal sealed record Tally(
    Guid MetricId,
    Guid RosterEntryId,
    Guid AthleteId,
    string FirstName,
    string LastName,
    short? JerseyNumber,
    Guid TeamId,
    string TeamName,
    int Total);

/// <summary>
/// Turns totals into a ranking.
/// </summary>
/// <remarks>
/// Kept apart from the query because the interesting part is not the sum, it
/// is what happens when two players have the same one — which in a leaderboard
/// is most of them.
/// </remarks>
internal static class Leaderboard
{
    /// <summary>
    /// The leaders of one metric, in order, with ties sharing a position.
    /// </summary>
    /// <param name="top">
    /// How many places to show. Ties are not cut in half: a board of three
    /// with four players level on second returns all of them, because
    /// publishing "second: Díaz" when three others scored the same is simply
    /// wrong.
    /// </param>
    public static IReadOnlyList<(int Position, Tally Player)> Rank(
        IEnumerable<Tally> tallies,
        int top)
    {
        var ordered = tallies
            .OrderByDescending(tally => tally.Total)

            // The last word, so a board is the same every time it is read.
            // Two players level on everything the ranking measures are still
            // two rows, and which is printed first has to be decided by
            // something that does not change between readings.
            .ThenBy(tally => tally.LastName, StringComparer.Ordinal)
            .ThenBy(tally => tally.FirstName, StringComparer.Ordinal)
            .ToList();

        var ranked = new List<(int Position, Tally Player)>(ordered.Count);

        var position = 0;
        var seen = 0;
        int? previous = null;

        foreach (var tally in ordered)
        {
            seen++;

            // Equal totals share a place, and the next distinct total skips
            // the places they used up: 1, 1, 3 rather than 1, 1, 2. Anything
            // else claims somebody finished second when nobody did.
            if (tally.Total != previous)
            {
                position = seen;
                previous = tally.Total;
            }

            // Read after the position is decided, so an entire tied place is
            // kept or dropped together.
            if (position > top)
            {
                break;
            }

            ranked.Add((position, tally));
        }

        return ranked;
    }
}
