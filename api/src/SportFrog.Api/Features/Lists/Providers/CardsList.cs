using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Who has been booked or sent off in a category — amarillas y rojas.</summary>
/// <remarks>
/// A sport with no card metrics in its catalog (basketball, volleyball,
/// wally) simply has no matching boards, which reads as an empty list rather
/// than an error — the same honesty <see cref="LeadersQuery.ForCategoryAsync"/>
/// already applies to a metric nobody has recorded yet. That is still what
/// <see cref="LoadAsync"/> does for a caller that asks directly; a catalog
/// built from <see cref="AppliesToAsync"/> does not offer the list to that
/// sport in the first place, today only football and futsal.
/// </remarks>
internal sealed class CardsList : IListProvider
{
    private static readonly string[] CardMetricCodes = ["yellow_card", "red_card"];

    public string Slug => "tarjetas";

    public string Label => "Tarjetas";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

    // Same reasoning as LeadersList, which this mirrors: a card is logged
    // against a Match, a judged category has none to log one against, and a
    // sport whose catalog carries neither CardMetricCodes has nothing this
    // could ever show regardless of score mode.
    public async Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scoreMode == ScoreMode.Judged)
        {
            return false;
        }

        return await database.SportMetrics
            .AsNoTracking()
            .AnyAsync(metric => metric.SportCode == sportCode && CardMetricCodes.Contains(metric.Code), cancellationToken);
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

        var sections = leaders.Boards
            .Where(board => CardMetricCodes.Contains(board.MetricCode, StringComparer.Ordinal))
            .Select(LeaderBoardSections.ToSection)
            .ToList();

        return new ListTable($"Tarjetas — {leaders.CategoryName}", null, LeaderBoardSections.Columns, sections);
    }
}
