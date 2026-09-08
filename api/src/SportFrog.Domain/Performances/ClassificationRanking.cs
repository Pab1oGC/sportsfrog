namespace SportFrog.Domain.Performances;

/// <summary>One team's place in a classification stage, before anything is ranked.</summary>
/// <param name="TeamId">The competing unit — one athlete, a pair or a trio.</param>
/// <param name="Score">Null until the team has performed.</param>
public sealed record PerformanceEntry(
    Guid PerformanceId,
    Guid TeamId,
    string TeamName,
    PerformanceStatus Status,
    int? Score);

/// <summary>
/// Turns a classification stage's performances into a ranking.
/// </summary>
/// <remarks>
/// A sibling of <see cref="Statistics.Leaderboard"/>, not a reuse of it:
/// that one ranks a roster entry against a metric total, and a
/// classification stage ranks a team — one athlete, a pair, or a trio —
/// against a judged score. Forcing this through <c>Tally</c> would mean
/// inventing a roster entry to stand for a team that might have three, which
/// answers a question nobody asked. What the two genuinely share is the
/// tie-sharing arithmetic, kept short enough here that duplicating it is
/// cheaper than bending one shape to fit two different rankings.
/// </remarks>
public static class ClassificationRanking
{
    /// <summary>
    /// Every team, ranked by score with ties sharing a place — teams that
    /// have not yet performed are listed after, unranked, rather than
    /// dropped or forced to a shared last place they have not earned.
    /// </summary>
    public static IReadOnlyList<(int? Position, PerformanceEntry Entry)> Rank(
        IEnumerable<PerformanceEntry> entries)
    {
        var scored = entries
            .Where(entry => entry.Score is not null)
            .OrderByDescending(entry => entry.Score)

            // The last word, so a board is the same every time it is read.
            .ThenBy(entry => entry.TeamName, StringComparer.Ordinal)
            .ToList();

        var pending = entries
            .Where(entry => entry.Score is null)
            .OrderBy(entry => entry.TeamName, StringComparer.Ordinal);

        var ranked = new List<(int? Position, PerformanceEntry Entry)>();

        var position = 0;
        var seen = 0;
        int? previous = null;

        foreach (var entry in scored)
        {
            seen++;

            // Equal scores share a place, and the next distinct one skips
            // the places they used up: 1, 1, 3 rather than 1, 1, 2.
            if (entry.Score != previous)
            {
                position = seen;
                previous = entry.Score;
            }

            ranked.Add((position, entry));
        }

        foreach (var entry in pending)
        {
            ranked.Add((null, entry));
        }

        return ranked;
    }
}
