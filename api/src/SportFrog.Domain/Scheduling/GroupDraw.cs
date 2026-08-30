using System.Security.Cryptography;

namespace SportFrog.Domain.Scheduling;

/// <summary>
/// A team as the group draw sees it: who it is, and which pot it sits in.
/// </summary>
/// <param name="Pot">
/// Null means no pot — the team is one of however many are drawn purely at
/// random, sharing no particular constraint with anybody else.
/// </param>
public sealed record SeededTeam(Guid TeamId, short? Pot);

/// <summary>One team's result: which group it landed in.</summary>
public sealed record GroupAssignment(Guid TeamId, string GroupLabel);

/// <summary>
/// Draws teams into groups — the half of a group stage that happens before
/// there is a single fixture to draw.
/// </summary>
/// <remarks>
/// One algorithm answers both a plain random draw and a seeded one, because
/// a plain draw is not a different procedure from a seeded draw — it is a
/// seeded draw where every team happens to share one pot. Shuffle the pot,
/// deal one team to each group in turn; do that pot by pot, and a real seed
/// keeps its promise — no group gets two teams out of the same pot before
/// every group has had one — while an unseeded draw, having only the one
/// pot, ends up exactly a fair shuffle across the groups.
///
/// A pot smaller than the group count is not a problem: some groups simply
/// do not get a team from it, which is the ordinary shape of "three
/// favourites marked, everyone else open." A pot larger than the group
/// count is a different matter — its promise cannot be kept no matter how
/// the dealing goes, at least one group is getting two teams out of it — and
/// that is refused outright rather than dealt out silently uneven. A pot
/// with a stray number in it, left over from a different draw or typed by
/// mistake, is exactly what this catches before it quietly reshapes a
/// result nobody asked for.
/// </remarks>
public static class GroupDraw
{
    /// <summary>
    /// Draws every team into one of <paramref name="groupCount"/> groups, or
    /// explains in one sentence why these numbers do not make a draw.
    /// </summary>
    /// <param name="respectPots">
    /// True is the ordinary case: no group ever gets two teams that share a
    /// pot. False draws every team from one shared pool regardless of what
    /// their own pot says — a pot recorded for reference, or for a format
    /// that deliberately lets its own favourites meet (a league phase built
    /// the way the newer Champions League format is, where two teams from
    /// the same pot can and do end up drawn against each other), rather than
    /// one this draw is meant to keep apart.
    /// </param>
    public static (IReadOnlyList<GroupAssignment>? Assignments, string? Problem) Draw(
        IReadOnlyList<SeededTeam> teams, int groupCount, bool respectPots = true)
    {
        if (groupCount < 2)
        {
            return (null, "Un sorteo de grupos necesita al menos dos grupos.");
        }

        if (teams.Count < groupCount)
        {
            return (null,
                $"Hay {teams.Count} equipo(s) compitiendo todavía y pediste {groupCount} " +
                "grupos: no alcanza ni uno por grupo.");
        }

        // A pot bigger than the group count cannot keep its own promise: some
        // group is getting two teams out of it no matter how the dealing
        // goes. Caught here, by name, rather than left for the dealing to
        // paper over — a bombo with a number nobody meant to leave on it is
        // exactly what this is for. Skipped when pots are not being
        // respected at all: there is no promise here to fail to keep.
        if (respectPots && Oversized(teams, groupCount) is { } oversized)
        {
            return (null,
                $"El bombo {oversized.Pot} tiene {oversized.Count} equipo(s), más que los " +
                $"{groupCount} grupos que pediste: no alcanza uno por grupo. Repartilos en otro " +
                "bombo, o subí la cantidad de grupos.");
        }

        var labels = Labels(groupCount);

        // Explicit pots first, lowest number first — the seeded half of the
        // draw — and whatever has no pot at all goes last, filling out
        // groups the way an unseeded team always has: however happens to be
        // left. With pots not respected, everyone is one shared pool — same
        // as if nobody had a pot at all.
        IEnumerable<IGrouping<short?, SeededTeam>> pots = respectPots
            ? teams.GroupBy(team => team.Pot).OrderBy(pot => pot.Key is null).ThenBy(pot => pot.Key)
            : teams.GroupBy(_ => (short?)null);

        var assignments = new List<GroupAssignment>(teams.Count);
        var cursor = 0;

        foreach (var pot in pots)
        {
            foreach (var team in Shuffled(pot))
            {
                assignments.Add(new GroupAssignment(team.TeamId, labels[cursor % groupCount]));
                cursor++;
            }
        }

        return (assignments, null);
    }

    /// <summary>
    /// The first pot, by number, that has more teams in it than there are
    /// groups to spread them over — or null if every pot fits.
    /// </summary>
    /// <remarks>
    /// Only ever the first one, not every one that is wrong: a single named
    /// example is what a person acts on. Somebody who fixes that pot and
    /// tries again finds out about the next one then, which is also how
    /// every other check in this codebase that could name several problems
    /// at once chooses to name one.
    /// </remarks>
    private static (short Pot, int Count)? Oversized(IReadOnlyList<SeededTeam> teams, int groupCount)
    {
        var oversized = teams
            .Where(team => team.Pot is not null)
            .GroupBy(team => team.Pot!.Value)
            .Select(pot => (Pot: pot.Key, Count: pot.Count()))
            .Where(pot => pot.Count > groupCount)
            .OrderBy(pot => pot.Pot)
            .ToList();

        return oversized.Count > 0 ? oversized[0] : null;
    }

    /// <summary>"A", "B", "C", ... — one per group, in order.</summary>
    private static string[] Labels(int groupCount)
    {
        var labels = new string[groupCount];

        for (var i = 0; i < groupCount; i++)
        {
            labels[i] = ((char)('A' + i)).ToString();
        }

        return labels;
    }

    /// <summary>
    /// A fair shuffle, not a convenient one. This is a draw somebody may be
    /// watching happen, and a shuffle that leans on a predictable generator
    /// is a draw that can be second-guessed.
    /// </summary>
    private static List<SeededTeam> Shuffled(IEnumerable<SeededTeam> teams)
    {
        var shuffled = teams.ToList();

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
