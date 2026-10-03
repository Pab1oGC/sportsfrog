using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>The standings table of a category, one section per group.</summary>
/// <remarks>
/// Wraps <see cref="StandingsQuery"/> rather than re-deriving a table: two
/// implementations of a standings table is two tables, and the one this
/// exports has to be the one the screen and the public page already agree
/// on.
/// </remarks>
internal sealed class StandingsList : IListProvider
{
    public string Slug => "posiciones";

    public string Label => "Tabla de posiciones";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

    // A judged category runs no bracket and no table — ClassificationList is
    // the right export for it instead, same distinction ChampionResolver
    // already draws by score mode before it looks at either.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(scoreMode != ScoreMode.Judged);

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is not { } categoryId)
        {
            return null;
        }

        if (await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken) is not { } standings)
        {
            return null;
        }

        var columns = Columns(standings.AllowsDraw);
        var sections = standings.Groups
            .Select(group => new ListSection(
                group.Label,
                [.. group.Rows.Select((row, index) => Row(index, row, standings.AllowsDraw))]))
            .ToList();

        return new ListTable($"Tabla de posiciones — {standings.CategoryName}", null, columns, sections);
    }

    // internal, not private: testable directly without a database, the same
    // convention Athletes.ReadAthletes.ListAsync already uses for logic
    // worth pinning on its own.
    /// <summary>
    /// "Empatados" only when this ruleset prices a draw at all — the same
    /// reason <see cref="StandingsResult.AllowsDraw"/> exists: a column that
    /// always reads zero is not a safeguard, it is noise on every row.
    /// </summary>
    internal static IReadOnlyList<ListColumn> Columns(bool allowsDraw)
    {
        List<ListColumn> columns =
        [
            new("Pos.", ListValueKind.Number),
            new("Equipo", ListValueKind.Text),
            new("PJ", ListValueKind.Number),
            new("PG", ListValueKind.Number),
        ];

        if (allowsDraw)
        {
            columns.Add(new ListColumn("PE", ListValueKind.Number));
        }

        columns.Add(new ListColumn("PP", ListValueKind.Number));
        columns.Add(new ListColumn("GF", ListValueKind.Number));
        columns.Add(new ListColumn("GC", ListValueKind.Number));
        columns.Add(new ListColumn("Dif.", ListValueKind.Number));
        columns.Add(new ListColumn("Pts.", ListValueKind.Number));

        return columns;
    }

    internal static IReadOnlyList<object?> Row(int index, StandingsRow row, bool allowsDraw)
    {
        List<object?> cells = [index + 1, row.TeamName, row.Played, row.Won];

        if (allowsDraw)
        {
            cells.Add(row.Drawn);
        }

        cells.Add(row.Lost);
        cells.Add(row.ScoreFor);
        cells.Add(row.ScoreAgainst);
        cells.Add(row.ScoreDifference);
        cells.Add(row.Points);

        return cells;
    }
}
