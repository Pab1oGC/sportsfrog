using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Domain.Performances;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Reads a category's classification stage — every team entered, and
/// whatever it has scored so far.
/// </summary>
/// <remarks>
/// Shared by every reader of a classification stage — the organization's own
/// <see cref="ReadPerformances"/>, <see cref="Draw.PromoteClassification"/>
/// deciding who advances, and the public portal's reading — for the same
/// reason <c>StandingsQuery</c> is shared by the organization's and the
/// public's standings: one query is one classification stage, and the day
/// two implementations of it disagree the argument is about which one the
/// league published.
/// </remarks>
internal static class PerformancesQuery
{
    public static Task<List<PerformanceEntry>> ForCategoryAsync(
        SportFrogDbContext database, Guid categoryId, CancellationToken cancellationToken) =>
        database.Performances
            .AsNoTracking()
            .Where(performance => performance.CategoryId == categoryId)
            .Select(performance => new PerformanceEntry(
                performance.Id,
                performance.TeamId,
                performance.Team!.Name,
                performance.Status,
                performance.Score))
            .ToListAsync(cancellationToken);
}
