namespace SportFrog.Domain.Scheduling;

/// <summary>
/// Which team fills one side of a <see cref="PlannedMatch"/>: one already
/// known when the draw was made, or the winner of an earlier match in the
/// same draw — named by its position in the list <see cref="Bracket.FullDraw"/>
/// returns, since none of those matches have a database id yet at the moment
/// the draw is computed.
/// </summary>
public readonly record struct BracketSlot
{
    public Guid? TeamId { get; }

    public int? SourceMatchIndex { get; }

    private BracketSlot(Guid? teamId, int? sourceMatchIndex)
    {
        TeamId = teamId;
        SourceMatchIndex = sourceMatchIndex;
    }

    /// <summary>A team the draw already knows — a real entrant, or a bye advancing without playing.</summary>
    public static BracketSlot Known(Guid teamId) => new(teamId, null);

    /// <summary>Whoever wins the match at this position in the same draw.</summary>
    public static BracketSlot FromWinnerOf(int matchIndex) => new(null, matchIndex);
}

/// <summary>
/// One match of a knockout drawn in full, before any of it has been played.
/// </summary>
public sealed record PlannedMatch(int Round, string Phase, BracketSlot Home, BracketSlot Away);
