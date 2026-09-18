namespace SportFrog.Domain.Scheduling;

/// <summary>
/// Pairs teams for a knockout, and names the round they are playing.
/// </summary>
/// <remarks>
/// Only one round can ever be drawn at a time, and that is the schema's doing
/// rather than a simplification: <c>home_team_id</c> is NOT NULL, so a
/// semi-final cannot exist before it is known who plays it. The bracket is
/// therefore built a round at a time, each one from the winners of the last.
///
/// Which is also the honest model. A bracket drawn to the end is a prediction
/// with empty spaces in it, and every one of those spaces is a match somebody
/// still has to play.
/// </remarks>
public static class Bracket
{
    /// <summary>
    /// The first round: who plays, and who sits it out.
    /// </summary>
    /// <remarks>
    /// A knockout needs a power of two to end in a single final. When the
    /// entry list is not one, the shortfall is made up with byes — the teams
    /// at the front of the list advance without playing, which is how a draw
    /// of eleven becomes a bracket of eight after the first round.
    ///
    /// Byes go to the front rather than being spread around because the order
    /// handed in is the only ranking there is. An organization that seeds its
    /// draw hands the teams in seeded, and the strongest sides skipping the
    /// first round is what seeding is for.
    /// </remarks>
    public static (IReadOnlyList<DrawnMatch> Matches, IReadOnlyList<Guid> Byes) FirstRound(
        IReadOnlyList<Guid> teams,
        int round = 1)
    {
        if (teams.Count < 2)
        {
            return ([], teams);
        }

        var byeCount = NextPowerOfTwo(teams.Count) - teams.Count;

        var byes = teams.Take(byeCount).ToList();
        var playing = teams.Skip(byeCount).ToList();

        var matches = new List<DrawnMatch>(playing.Count / 2);

        for (var i = 0; i < playing.Count; i += 2)
        {
            matches.Add(new DrawnMatch(round, playing[i], playing[i + 1]));
        }

        return (matches, byes);
    }

    /// <summary>
    /// Every round of a knockout, drawn at once instead of one at a time.
    /// </summary>
    /// <remarks>
    /// <see cref="FirstRound"/> still decides who plays and who byes in round
    /// one — this only continues past it, pairing round two onward from slots
    /// that are not teams yet but positions in this very list: "whoever wins
    /// the match at index 3". Nothing here knows a database; a caller that
    /// wants every round's calendar and venue booked before a ball is kicked
    /// assigns its own row ids against these positions.
    ///
    /// Byes are folded into round two exactly once, the same rule
    /// <c>AdvanceBracket</c> applies a round at a time: they go first, ahead
    /// of the winners, keeping the order the opening draw gave them. Every
    /// round after that is already a power of two wide, which is why it never
    /// needs another bye.
    /// </remarks>
    public static IReadOnlyList<PlannedMatch> FullDraw(IReadOnlyList<Guid> teams)
    {
        if (teams.Count < 2)
        {
            return [];
        }

        var (firstRoundMatches, byes) = FirstRound(teams);

        var plan = new List<PlannedMatch>(firstRoundMatches.Select(match => new PlannedMatch(
            1,
            Phase(firstRoundMatches.Count, 1),
            BracketSlot.Known(match.HomeTeamId),
            BracketSlot.Known(match.AwayTeamId))));

        var advancing = new List<BracketSlot>(byes.Select(BracketSlot.Known));
        advancing.AddRange(Enumerable.Range(0, firstRoundMatches.Count).Select(BracketSlot.FromWinnerOf));

        var round = 2;

        while (advancing.Count > 1)
        {
            var roundStart = plan.Count;
            var matchesThisRound = advancing.Count / 2;
            var phase = Phase(matchesThisRound, round);
            var next = new List<BracketSlot>(matchesThisRound);

            for (var i = 0; i < advancing.Count; i += 2)
            {
                plan.Add(new PlannedMatch(round, phase, advancing[i], advancing[i + 1]));
                next.Add(BracketSlot.FromWinnerOf(roundStart + next.Count));
            }

            advancing = next;
            round++;
        }

        return plan;
    }

    /// <summary>
    /// What to call a round of this many matches.
    /// </summary>
    /// <remarks>
    /// Named rather than numbered because that is how a knockout is talked
    /// about: nobody says "round four", they say the semi-final. In Spanish
    /// for the same reason the sports catalog is — it is printed on a fixture
    /// and read by the people playing it.
    ///
    /// Anything larger than sixteenths falls back to a number, which is the
    /// point at which the names stop being ones anybody uses.
    /// </remarks>
    public static string Phase(int matches, int round) => matches switch
    {
        1 => "final",
        2 => "semifinal",
        4 => "cuartos",
        8 => "octavos",
        16 => "dieciseisavos",
        _ => $"ronda {round}",
    };

    /// <summary>
    /// The bracket size this entry list rounds up to.
    /// </summary>
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
