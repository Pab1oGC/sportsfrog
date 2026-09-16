namespace SportFrog.Domain.Matches;

/// <summary>
/// Who won a decided match, and by which of the three ways a match decides
/// one.
/// </summary>
/// <remarks>
/// Pulled out from where this used to live inline — <c>AdvanceBracket</c>,
/// drawing the next round from the winners of the last — because the same
/// question is asked from a second place now: the public page's own answer
/// to "who is the champion", for the one category a finished knockout has a
/// single decided final for. Two readings of the same rule is two rules, and
/// this is the one place both now ask it.
///
/// Not a database call and not aware of a bracket: it takes a match's own
/// numbers and answers for that match alone. What round it was, or what
/// happens to the winner next, is the caller's business.
/// </remarks>
public static class MatchWinner
{
    /// <summary>
    /// The winning side, or null when the match has no result yet or ended
    /// level with no shootout recorded to break it.
    /// </summary>
    /// <remarks>
    /// Checked in this order because that is the order a result actually
    /// resolves in: an award settles it outright regardless of any score
    /// entered alongside it, an unequal score settles it on its own, and a
    /// level score is decided only if a shootout was recorded — its absence
    /// means the tie was never actually broken, not that neither side won.
    /// </remarks>
    public static Guid? Resolve(
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? walkoverTeamId,
        int? homeTotal,
        int? awayTotal,
        short? penaltyHomeScore,
        short? penaltyAwayScore)
    {
        if (walkoverTeamId is { } awarded)
        {
            return awarded;
        }

        if (homeTotal is null || awayTotal is null)
        {
            return null;
        }

        if (homeTotal != awayTotal)
        {
            return homeTotal > awayTotal ? homeTeamId : awayTeamId;
        }

        if (penaltyHomeScore is { } penaltyHome && penaltyAwayScore is { } penaltyAway)
        {
            return penaltyHome > penaltyAway ? homeTeamId : awayTeamId;
        }

        return null;
    }
}
