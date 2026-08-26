namespace SportFrog.Domain.Scheduling;

/// <summary>One fixture the draw produced, before it has a date or a pitch.</summary>
public sealed record DrawnMatch(int Round, Guid HomeTeamId, Guid AwayTeamId);

/// <summary>
/// Draws an all-play-all calendar.
/// </summary>
/// <remarks>
/// The circle method, which is the standard answer and worth naming because
/// the obvious alternative — pair everyone with everyone and then try to slice
/// the result into rounds — does not work: a round has to use each team
/// exactly once, and that constraint is what the rotation solves for free.
///
/// One team is held still and the rest rotate around it. After every rotation
/// the list is folded in half and each pair faces off, which by construction
/// gives every team exactly one opponent per round and every opponent exactly
/// once over the whole draw.
///
/// No database and no clock: teams go in, fixtures come out. A calendar is
/// something organizers argue with, so it has to be something a person can
/// check by hand on a napkin.
/// </remarks>
public static class RoundRobin
{
    /// <summary>
    /// Every fixture, round by round.
    /// </summary>
    /// <param name="legs">
    /// One for a single round of matches, two for home and away. The second
    /// leg is the first replayed with the sides swapped, which is what a
    /// league means by it — the same pairings in the same order, at the other
    /// team's ground.
    /// </param>
    public static IReadOnlyList<DrawnMatch> Draw(IReadOnlyList<Guid> teams, int legs)
    {
        if (teams.Count < 2)
        {
            return [];
        }

        var first = SingleLeg(teams);

        if (legs < 2)
        {
            return first;
        }

        var rounds = first.Max(match => match.Round);

        return
        [
            .. first,
            .. first.Select(match => new DrawnMatch(
                match.Round + rounds, match.AwayTeamId, match.HomeTeamId)),
        ];
    }

    /// <summary>
    /// One round of everyone against everyone.
    /// </summary>
    private static List<DrawnMatch> SingleLeg(IReadOnlyList<Guid> teams)
    {
        // An odd number of teams cannot be paired off, so a ghost joins the
        // circle and whoever draws it sits the round out. It never reaches a
        // fixture: the pairing that contains it is dropped.
        var circle = new List<Guid>(teams);
        var odd = circle.Count % 2 == 1;

        if (odd)
        {
            circle.Add(Guid.Empty);
        }

        var size = circle.Count;
        var matches = new List<DrawnMatch>();

        for (var round = 1; round < size; round++)
        {
            for (var i = 0; i < size / 2; i++)
            {
                var home = circle[i];
                var away = circle[size - 1 - i];

                if (home == Guid.Empty || away == Guid.Empty)
                {
                    // The team drawn against the ghost rests this round.
                    continue;
                }

                // Alternating who is listed first keeps a team from playing
                // every fixture of the draw at home. Without it the fixed team
                // in particular would host all of them, which is the classic
                // way a generated calendar reads as unfair.
                matches.Add(round % 2 == 0
                    ? new DrawnMatch(round, away, home)
                    : new DrawnMatch(round, home, away));
            }

            Rotate(circle);
        }

        return matches;
    }

    /// <summary>
    /// Turns the circle one place, holding the first team still.
    /// </summary>
    /// <remarks>
    /// The last team moves to second and everyone else shifts along. Holding
    /// one still is what makes the rotation cover every pairing: with nobody
    /// fixed the whole circle would turn together and the same teams would
    /// face each other every round.
    /// </remarks>
    private static void Rotate(List<Guid> circle)
    {
        var last = circle[^1];
        circle.RemoveAt(circle.Count - 1);
        circle.Insert(1, last);
    }
}
