using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Who has been booked or sent off in a category — amarillas y rojas.</summary>
/// <remarks>
/// A sport with no card metrics in its catalog (basketball, volleyball,
/// wally) simply has no matching boards, which reads as an empty list rather
/// than an error — the same honesty <see cref="LeadersQuery.ForCategoryAsync"/>
/// already applies to a metric nobody has recorded yet.
/// </remarks>
internal sealed class CardsList : IListProvider
{
    private static readonly string[] CardMetricCodes = ["yellow_card", "red_card"];

    public string Slug => "tarjetas";

    public string Label => "Tarjetas";

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

        var sections = leaders.Boards
            .Where(board => CardMetricCodes.Contains(board.MetricCode, StringComparer.Ordinal))
            .Select(LeaderBoardSections.ToSection)
            .ToList();

        return new ListTable($"Tarjetas — {leaders.CategoryName}", null, LeaderBoardSections.Columns, sections);
    }
}
