namespace SportFrog.Domain.Standings;

/// <summary>
/// Decides who moves on from a finished group stage into a knockout, and in
/// what order.
/// </summary>
/// <remarks>
/// Two questions, kept apart from a third that belongs elsewhere. Who
/// qualifies, and in what seeded order, is what this answers. How many of
/// them get a bye and who plays whom in round one is
/// <see cref="Scheduling.Bracket.FirstRound"/>'s job, once handed the order
/// this settles on — this file never draws a match.
///
/// Every group has already been through <see cref="Tiebreaking"/>, so every
/// row's place inside its own group is final. Ranking a team from one group
/// against one from another is a different question — they never played
/// each other — and is answered here close to how a table answers it within
/// a group: points, then goal difference, then goals scored. Head-to-head is
/// left out on purpose; it cannot mean anything between two teams with no
/// match between them.
/// </remarks>
public static class GroupStageAdvancement
{
    /// <param name="Seeded">
    /// Every qualifier, strongest first, arranged so that
    /// <see cref="Scheduling.Bracket.FirstRound"/> does not pair two teams
    /// the group stage already put in the same table — unless the numbers
    /// themselves leave no other match to give them.
    /// </param>
    /// <param name="Direct">How many qualified by finishing high enough in their own group.</param>
    /// <param name="Wildcards">How many qualified by out-ranking the other groups' next-best.</param>
    /// <param name="RepeatedMatchups">
    /// How many of the pairings this seeding produces are a rematch of the
    /// group stage — normally zero, and only ever positive when the numbers
    /// leave no other way to pair everyone.
    /// </param>
    public sealed record Plan(
        IReadOnlyList<Guid> Seeded, int Direct, int Wildcards, int RepeatedMatchups);

    /// <summary>
    /// Works out who advances, or explains in one sentence why these numbers
    /// do not fit this group stage.
    /// </summary>
    public static (Plan? Plan, string? Problem) Build(
        IReadOnlyList<StandingsGroup> groups,
        int qualifiersPerGroup,
        int bestThirdPlaced)
    {
        var real = groups
            .Where(group => group.Label is not null)
            .OrderBy(group => group.Label, StringComparer.Ordinal)
            .ToList();

        if (real.Count == 0)
        {
            return (null, "Esta categoría no tiene grupos: no hay de dónde clasificar a nadie.");
        }

        if (real.FirstOrDefault(group => group.Rows.Count < qualifiersPerGroup) is { } short_)
        {
            return (null,
                $"El grupo {short_.Label} tiene {short_.Rows.Count} equipo(s), menos de los " +
                $"{qualifiersPerGroup} que clasifican directo por grupo.");
        }

        // Whoever finished right after the direct cutoff in a group with a
        // team to spare is the pool: the "best thirds" a two-per-group
        // knockout is famous for, generalized to whatever rank the cutoff
        // actually is.
        var wildcardPool = real
            .Where(group => group.Rows.Count > qualifiersPerGroup)
            .Select(group => group.Rows[qualifiersPerGroup])
            .ToList();

        if (bestThirdPlaced > wildcardPool.Count)
        {
            return (null,
                $"Solo {wildcardPool.Count} grupo(s) tienen un equipo de sobra para pelear un " +
                $"cupo de mejor ubicado; pediste {bestThirdPlaced}.");
        }

        var direct = qualifiersPerGroup * real.Count;

        if (direct + bestThirdPlaced < 2)
        {
            return (null,
                "Con estos números clasifica menos de dos equipos; no hay eliminatoria que sortear.");
        }

        var wildcards = Rank(wildcardPool).Take(bestThirdPlaced).ToList();

        var seeded = new List<(Guid Team, string? Group)>();

        for (var rank = 0; rank < qualifiersPerGroup; rank++)
        {
            seeded.AddRange(real.Select(group => (group.Rows[rank].TeamId, group.Label)));
        }

        seeded.AddRange(wildcards.Select(row => (row.TeamId, row.GroupLabel)));

        var repeats = Reseed(seeded);

        return (new Plan([.. seeded.Select(entry => entry.Team)], direct, wildcards.Count, repeats), null);
    }

    /// <summary>
    /// Orders a pool of teams that never played each other — one row per
    /// group, level on nothing but a table they were never both in.
    /// </summary>
    /// <remarks>
    /// Points, then goal difference, then goals scored: the same order a
    /// table reads in, minus head-to-head, which needs two teams to have
    /// met. What is left runs out too, eventually — a genuine tie between
    /// two teams from different groups, level on everything this can look
    /// at — and is settled the way a table settles it: by name, so the order
    /// does not depend on which reading asked for it.
    /// </remarks>
    private static IEnumerable<StandingsRow> Rank(IEnumerable<StandingsRow> pool) =>
        pool
            .OrderByDescending(row => row.Points)
            .ThenByDescending(row => row.ScoreDifference)
            .ThenByDescending(row => row.ScoreFor)
            .ThenByDescending(row => row.Won)
            .ThenBy(row => row.TeamName, StringComparer.Ordinal);

    /// <summary>
    /// Swaps a wildcard out of the way of its own group, wherever the byes
    /// this list will get leave the two of them facing each other. Answers
    /// how many it could not fix.
    /// </summary>
    /// <remarks>
    /// Every direct qualifier is already safe by construction: within one
    /// rank tier every entry is a different group, and two tiers only ever
    /// meet at a boundary that lands on two different groups as well, as
    /// long as more than one group is playing. The one seat that can still
    /// collide with a group's own qualifier is a wildcard drawn from that
    /// same group — its runner-up-or-lower finish came from playing the very
    /// team it might now be asked to play again, immediately.
    ///
    /// Byes are computed the same way <see cref="Scheduling.Bracket.FirstRound"/>
    /// computes them — the front of the list, until what remains is a power
    /// of two — deliberately kept as a second copy of that one formula
    /// rather than a shared call, so that primitive stays exactly what it
    /// is: generic pairing, with no idea a group ever existed. This has to
    /// know which pairs actually happen before it can fix one of them.
    /// </remarks>
    private static int Reseed(List<(Guid Team, string? Group)> seeded)
    {
        var byeCount = NextPowerOfTwo(seeded.Count) - seeded.Count;
        var repeats = 0;

        for (var i = byeCount; i + 1 < seeded.Count; i += 2)
        {
            if (seeded[i].Group != seeded[i + 1].Group)
            {
                continue;
            }

            var fixed_ = false;

            for (var j = i + 2; j < seeded.Count; j++)
            {
                var relative = j - byeCount;
                var partnerOfJ = relative % 2 == 0 ? j + 1 : j - 1;

                if (seeded[j].Group == seeded[i].Group
                    || (partnerOfJ < seeded.Count && seeded[partnerOfJ].Group == seeded[i + 1].Group))
                {
                    continue;
                }

                (seeded[i + 1], seeded[j]) = (seeded[j], seeded[i + 1]);
                fixed_ = true;
                break;
            }

            if (!fixed_)
            {
                // Nothing left in the whole list could take this seat
                // without creating the same problem somewhere else — every
                // remaining team is from this pair's own group, or would
                // hand its own partner a rematch instead. The numbers
                // themselves leave no better pairing, which is not this
                // function's mistake to fix, only to report.
                repeats++;
            }
        }

        return repeats;
    }

    private static int NextPowerOfTwo(int count)
    {
        var size = 1;

        while (size < count)
        {
            size *= 2;
        }

        return size;
    }
}
