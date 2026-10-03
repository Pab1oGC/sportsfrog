using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Who leads a category in whatever moves its score — a goal, a basket, a point.</summary>
/// <remarks>
/// Wraps <see cref="LeadersQuery"/> rather than re-deriving a scoring board:
/// two implementations of a top scorer list is two lists, and the one this
/// exports has to be the one <c>Features.Statistics.ReadLeaders</c> already
/// shows on screen.
///
/// Named and labeled sport-neutrally ("Máximos anotadores", not
/// "Goleadores" — a football-only word for the same idea): a volleyball or
/// basketball competition is just as likely to ask for this board as a
/// football one is, and this is the one list in the feature every team
/// sport with a scoring metric shares.
/// </remarks>
internal sealed class LeadersList : IListProvider
{
    public string Slug => "goleadores";

    public string Label => "Máximos anotadores";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

    /// <summary>
    /// LeadersQuery tallies events logged against a Match — a judged
    /// category logs none, so it never has a board to show. Narrower still:
    /// a sport whose own metrics never affect the score (volleyball's point
    /// is tallied by set, not summed into a total) has nothing for the
    /// "only the boards that move the score" filter below to keep either —
    /// <see cref="LoadAsync"/> would resolve the category and still hand
    /// back an empty table, which is honest but not worth cataloging.
    /// </summary>
    public async Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scoreMode == ScoreMode.Judged)
        {
            return false;
        }

        return await database.SportMetrics
            .AsNoTracking()
            .AnyAsync(
                metric => metric.SportCode == sportCode && metric.IsRankable && metric.AffectsScore,
                cancellationToken);
    }

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is not { } categoryId)
        {
            return null;
        }

        if (await LeadersQuery.ForCategoryAsync(database, categoryId, LeaderBoardSections.Unlimited, cancellationToken)
            is not { } leaders)
        {
            return null;
        }

        // Only the boards that move the score — a "goleadores" list of
        // fouls or rebounds would not be one. A combined points board still
        // counts: it is the same scoring, just more than one metric worth of
        // it added together.
        var sections = leaders.Boards
            .Where(board => board.AffectsScore)
            .Select(LeaderBoardSections.ToSection)
            .ToList();

        return new ListTable($"Máximos anotadores — {leaders.CategoryName}", null, LeaderBoardSections.Columns, sections);
    }
}
