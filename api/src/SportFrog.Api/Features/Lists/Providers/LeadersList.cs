using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Who leads a category in whatever moves its score — a goal, a basket, a point.</summary>
/// <remarks>
/// Wraps <see cref="LeadersQuery"/> rather than re-deriving a scoring board:
/// two implementations of a top scorer list is two lists, and the one this
/// exports has to be the one <c>Features.Statistics.ReadLeaders</c> already
/// shows on screen.
/// </remarks>
internal sealed class LeadersList : IListProvider
{
    public string Slug => "goleadores";

    public string Label => "Goleadores";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

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

        return new ListTable($"Goleadores — {leaders.CategoryName}", null, LeaderBoardSections.Columns, sections);
    }
}
