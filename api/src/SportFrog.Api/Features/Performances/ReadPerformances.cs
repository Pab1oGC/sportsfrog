using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Performances;

/// <summary>
/// Reads a category's classification stage, ranked by judged score.
/// </summary>
public static class ReadPerformances
{
    public sealed record Row(
        Guid PerformanceId,
        Guid TeamId,
        string TeamName,
        PerformanceStatus Status,
        int? Score,

        /// <summary>Null while the team has not yet performed — see <see cref="ClassificationRanking"/>.</summary>
        int? Position,

        Guid? VenueSpaceId,
        string? VenueName,
        string? SpaceName,

        /// <summary>The day this team performs. See <see cref="Performance.ScheduledOn"/>.</summary>
        DateOnly? ScheduledOn,

        /// <summary>This team's turn in that mat's running order for that day.</summary>
        short? OrderNumber);

    public sealed record Response(IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapReadPerformances(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/categories/{categoryId:guid}/performances", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadPerformances))
            .WithSummary("Reads a category's classification stage, ranked by judged score.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid categoryId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var entries = await PerformancesQuery.ForCategoryAsync(database, categoryId, cancellationToken);

        if (entries.Count == 0
            && !await database.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return Results.NotFound();
        }

        var ranked = ClassificationRanking.Rank(entries);

        // Where and when, kept apart from PerformancesQuery: ranking a
        // classification stage and placing it in a running order are
        // different questions, the same reason CalendarPlacement is
        // separate from the draw — and PerformancesQuery is shared by two
        // other readers (PromoteClassification, the public portal) that
        // have no use for either.
        var schedule = await database.Performances
            .AsNoTracking()
            .Where(performance => performance.CategoryId == categoryId)
            .Select(performance => new
            {
                performance.Id,
                performance.VenueSpaceId,
                VenueName = performance.VenueSpace!.Venue!.Name,
                SpaceName = performance.VenueSpace!.Name,
                performance.ScheduledOn,
                performance.OrderNumber,
            })
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        return Results.Ok(new Response(
            [.. ranked.Select(item =>
            {
                schedule.TryGetValue(item.Entry.PerformanceId, out var slot);

                return new Row(
                    item.Entry.PerformanceId,
                    item.Entry.TeamId,
                    item.Entry.TeamName,
                    item.Entry.Status,
                    item.Entry.Score,
                    item.Position,
                    slot?.VenueSpaceId,
                    slot?.VenueName,
                    slot?.SpaceName,
                    slot?.ScheduledOn,
                    slot?.OrderNumber);
            })]));
    }
}
