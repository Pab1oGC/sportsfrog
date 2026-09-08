using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Caching;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// A judged category's classification stage, for every category of a
/// published competition that opened one.
/// </summary>
/// <remarks>
/// Poomsae's counterpart to <see cref="ReadPublicTables"/>: a category
/// decided by judges has no standings table to read —
/// <see cref="ReadPublicCompetition.Sections.Classification"/> is what tells
/// a page to ask here instead of at <c>/standings</c>. Built from the same
/// <see cref="Performances.PerformancesQuery"/> and the same
/// <see cref="ClassificationRanking"/> the organization's own
/// <see cref="Performances.ReadPerformances"/> ranks with, for the reason
/// every public reading in this file shares one implementation with its
/// private counterpart: two rankings of the same stage is two rankings, and
/// the day they disagree the question is not which one is right.
///
/// A category that never opened a classification stage is left out of the
/// response entirely, the same way <see cref="ReadPublicTables"/> leaves out
/// a category its query finds nothing built for — there is nothing to
/// publish, not an empty thing to publish.
///
/// Cached past the publish check — see <see cref="IPublicQueryCache"/> —
/// because a live judged bout is exactly the case a public page gets
/// refreshed against repeatedly by a crowd watching one score come in, and
/// the answer changes only when a judge's score is recorded, not on every
/// refresh a phone in the stands makes.
/// </remarks>
public static class ReadPublicClassification
{
    public sealed record Row(
        Guid PerformanceId,
        Guid TeamId,
        string TeamName,
        PerformanceStatus Status,
        int? Score,

        /// <summary>Null while the team has not yet performed — see <see cref="ClassificationRanking"/>.</summary>
        int? Position);

    public sealed record CategoryClassification(Guid CategoryId, string CategoryName, IReadOnlyList<Row> Rows);

    public sealed record Response(IReadOnlyList<CategoryClassification> Categories);

    public static IEndpointRouteBuilder MapReadPublicClassification(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/classification", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicClassification))
            .WithSummary(
                "Reads the classification stage of every judged category of a published competition.");

        return routes;
    }

    /// <summary>
    /// Carries "classification is not published" back through a reader
    /// whose own null already means "the address does not resolve" — same
    /// device, and the same reason, as <c>ReadPublicTables.Gate</c>.
    /// </summary>
    private sealed record Gate(Response? Payload);

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        IPublicQueryCache cache,
        CancellationToken cancellationToken)
    {
        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                var settings = await database.Competitions
                    .AsNoTracking()
                    .Where(competition => competition.Id == resolved.CompetitionId)
                    .Select(competition => competition.Settings)
                    .SingleAsync(cancellationToken);

                if (!(settings.Public ?? new PublicSettings()).ShowClassification)
                {
                    return new Gate(null);
                }

                // The cache sits here, past the publish check and inside the
                // transaction the reader already opened — not in front of
                // the whole request. Resolving the address is one indexed
                // function call; what is worth sparing a live event's worth
                // of refreshes is the per-category walk below, which is the
                // part that actually grows with how many categories and
                // performances a competition has.
                var payload = await cache.GetOrCreateAsync(
                    PublicCacheKey.Classification(resolved.CompetitionId),
                    _ => BuildAsync(database, resolved.CompetitionId, cancellationToken),
                    cancellationToken);

                return new Gate(payload);
            },
            cancellationToken);

        return page?.Payload is null ? Results.NotFound() : Results.Ok(page.Payload);
    }

    private static async Task<Response> BuildAsync(
        SportFrogDbContext database, Guid competitionId, CancellationToken cancellationToken)
    {
        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => category.CompetitionId == competitionId)
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(cancellationToken);

        var stages = new List<CategoryClassification>(categories.Count);

        foreach (var category in categories)
        {
            var entries = await Performances.PerformancesQuery.ForCategoryAsync(
                database, category.Id, cancellationToken);

            if (entries.Count == 0)
            {
                continue;
            }

            var ranked = ClassificationRanking.Rank(entries);

            stages.Add(new CategoryClassification(
                category.Id,
                category.Name,
                [.. ranked.Select(item => new Row(
                    item.Entry.PerformanceId,
                    item.Entry.TeamId,
                    item.Entry.TeamName,
                    item.Entry.Status,
                    item.Entry.Score,
                    item.Position))]));
        }

        return new Response(stages);
    }
}
