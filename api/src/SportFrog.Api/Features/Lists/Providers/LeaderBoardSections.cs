using SportFrog.Api.Features.Statistics;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>
/// Turns a <see cref="RankedBoard"/> — one metric's ranking, or the combined
/// points board — into the rows a list export carries.
/// </summary>
/// <remarks>
/// Shared by every provider built over <see cref="LeadersQuery"/>, so a goals
/// board and a cards board read the same columns the same way, and the day a
/// third one is added it reads them the same way too.
/// </remarks>
internal static class LeaderBoardSections
{
    /// <summary>
    /// Passed to <see cref="LeadersQuery.ForCategoryAsync"/> in place of the
    /// 1–100 a screen asks for: an export needs every player a board has, not
    /// the page one happens to show. Safe because <see cref="Leaderboard.Rank"/>
    /// only ever checks a position against this ceiling, and no board will
    /// ever have that many tied places.
    /// </summary>
    public const int Unlimited = int.MaxValue;

    public static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Pos.", ListValueKind.Number),
        new ListColumn("Jugador", ListValueKind.Text),
        new ListColumn("Dorsal", ListValueKind.Text),
        new ListColumn("Equipo", ListValueKind.Text),
        new ListColumn("Total", ListValueKind.Number),
    ];

    public static ListSection ToSection(RankedBoard board) =>
        new(board.MetricLabel, [.. board.Leaders.Select(Row)]);

    private static IReadOnlyList<object?> Row((int Position, Tally Player) leader) =>
    [
        leader.Position,
        $"{leader.Player.LastName}, {leader.Player.FirstName}",
        leader.Player.JerseyNumber?.ToString() ?? "-",
        leader.Player.TeamName,
        leader.Player.Total,
    ];
}
