namespace SportFrog.Domain.Scheduling;

/// <summary>
/// Kyorugi's own way of deciding third place: not one match between two
/// semifinal losers, but a second chance for everyone the two finalists beat
/// on their way to the final.
/// </summary>
/// <remarks>
/// The World Taekwondo rule this models: once the final's two competitors are
/// known, whoever lost to either of them — at any round, not only the
/// semifinal — has the right to fight again, for one of two bronze medals
/// rather than a single third place. The two finalists never share an
/// opponent (a single-elimination bracket is a tree, and losing ends a run),
/// so the field splits cleanly into the two finalists' own halves, and each
/// half settles its own bronze independently of the other.
///
/// Within a half, the entrants are not seeded into a symmetric bracket —
/// there is no reason to, since there are rarely a power of two of them and
/// nobody is left to draw a bye against. Instead they form a ladder: the
/// competitor the finalist beat earliest (round one) plays first, the winner
/// meets whoever the finalist beat next, and so on, until the last match is
/// against whoever pushed the finalist hardest — the semifinal loser, who
/// enters last because they are the only one who has not already had to win
/// once in this same repechage to stay in it. That last match is the one
/// that actually decides the bronze.
///
/// <see cref="BuildHalf"/> takes the opponents a single finalist beat,
/// oldest round first, and returns the ladder to run — or, when there is at
/// most one of them, the fact that nobody needs to play at all.
/// </remarks>
public static class Repechage
{
    /// <summary>An ordinary rung of the ladder: win it to keep fighting for bronze.</summary>
    public const string LadderPhase = "repechaje";

    /// <summary>The ladder's last rung — its winner is the bronze medalist for this half.</summary>
    public const string BronzePhase = "repechaje bronce";

    /// <summary>Both phases a repechage match can carry, closed the same way <see cref="CompetitionFormat.All"/> is.</summary>
    public static readonly IReadOnlySet<string> Phases = new HashSet<string>(StringComparer.Ordinal)
    {
        LadderPhase,
        BronzePhase,
    };

    /// <summary>
    /// One finalist's half of the repechage.
    /// </summary>
    /// <param name="Matches">The ladder to play, oldest opponent first. Empty when there is nobody to play at all.</param>
    /// <param name="AutomaticBronze">
    /// Set when exactly one competitor is eligible: there is nobody left for
    /// them to play, so they take the bronze outright, the same way a lone
    /// bye advances without a match. Null whenever <see cref="Matches"/> is
    /// what actually decides it — including the degenerate case of zero
    /// eligible competitors, where there is no bronze to award on this side
    /// at all.
    /// </param>
    public sealed record Half(IReadOnlyList<PlannedMatch> Matches, Guid? AutomaticBronze);

    /// <summary>
    /// Builds one finalist's half of the ladder.
    /// </summary>
    /// <param name="beatenInRoundOrder">
    /// Every competitor this finalist beat on the way to the final, ordered
    /// from the earliest round to the latest (the semifinal last). Each is
    /// assumed to have lost to no one else this bracket ever sent to a
    /// repechage — true by construction, since a single-elimination bracket
    /// never lets the same competitor reach two different finalists.
    /// </param>
    public static Half BuildHalf(IReadOnlyList<Guid> beatenInRoundOrder)
    {
        if (beatenInRoundOrder.Count == 0)
        {
            return new Half([], null);
        }

        if (beatenInRoundOrder.Count == 1)
        {
            return new Half([], beatenInRoundOrder[0]);
        }

        var plan = new List<PlannedMatch>(beatenInRoundOrder.Count - 1);
        var previous = BracketSlot.Known(beatenInRoundOrder[0]);

        for (var i = 1; i < beatenInRoundOrder.Count; i++)
        {
            var isLastRung = i == beatenInRoundOrder.Count - 1;

            plan.Add(new PlannedMatch(
                i,
                isLastRung ? BronzePhase : LadderPhase,
                previous,
                BracketSlot.Known(beatenInRoundOrder[i])));

            previous = BracketSlot.FromWinnerOf(plan.Count - 1);
        }

        return new Half(plan, null);
    }
}
