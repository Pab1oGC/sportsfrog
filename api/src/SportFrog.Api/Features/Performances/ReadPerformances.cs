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
        int? Position);

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

        return Results.Ok(new Response(
            [.. ranked.Select(item => new Row(
                item.Entry.PerformanceId,
                item.Entry.TeamId,
                item.Entry.TeamName,
                item.Entry.Status,
                item.Entry.Score,
                item.Position))]));
    }
}
