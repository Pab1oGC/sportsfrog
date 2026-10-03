using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>The full ranking of a judged category's classification stage.</summary>
/// <remarks>
/// Wraps <see cref="PerformancesQuery"/> and <see cref="ClassificationRanking"/>
/// rather than re-deriving a ranking: the same two
/// <see cref="ChampionResolver"/> already reads from, to answer one position
/// instead of all of them. Two implementations of a classification ranking
/// is two rankings, and the one this exports has to be the one
/// <c>Performances.ReadPerformances</c> already shows on screen.
///
/// Null for a category that is not judged — <c>StandingsList</c> and
/// <c>LeadersList</c> are the right export for a team sport's category, the
/// same distinction <see cref="ChampionResolver"/> draws by
/// <see cref="ScoreMode"/> before it ever looks at a bracket or a table.
/// </remarks>
internal sealed class ClassificationList : IListProvider
{
    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Pos.", ListValueKind.Text),
        new ListColumn("Deportista / Equipo", ListValueKind.Text),
        new ListColumn("Puntaje", ListValueKind.Number),
    ];

    public string Slug => "clasificacion";

    public string Label => "Clasificación (categorías juzgadas)";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

    // The one list that exists for a judged category precisely because
    // StandingsList does not apply to it — see this class's own remarks.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(scoreMode == ScoreMode.Judged);

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is not { } categoryId)
        {
            return null;
        }

        var category = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new { candidate.Name, candidate.Competition!.SportCode })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var scoreMode = await database.Sports
            .AsNoTracking()
            .Where(sport => sport.Code == category.SportCode)
            .Select(sport => (ScoreMode?)sport.ScoreMode)
            .SingleOrDefaultAsync(cancellationToken);

        if (scoreMode != ScoreMode.Judged)
        {
            return null;
        }

        var entries = await PerformancesQuery.ForCategoryAsync(database, categoryId, cancellationToken);
        var ranked = ClassificationRanking.Rank(entries);

        var rows = ranked
            .Select(entry => (IReadOnlyList<object?>)
            [
                entry.Position?.ToString() ?? "-",
                entry.Entry.TeamName,
                entry.Entry.Score,
            ])
            .ToList();

        return new ListTable($"Clasificación — {category.Name}", null, Columns, [new ListSection(null, rows)]);
    }
}
