using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Features.Statistics;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// The standings and the leaderboards of a published competition.
/// </summary>
/// <remarks>
/// Both are built by the same code the organization's own endpoints use.
/// That is deliberate and is the whole design of this file: two
/// implementations of a standings table is two tables, and the day they
/// disagree the argument is not about which is right but about which one the
/// league published.
///
/// Both are also gated by the competition's settings, and a section that is
/// not published is answered as not found rather than as forbidden. "This
/// exists but you may not see it" is itself information about a competition
/// that chose to keep something private.
/// </remarks>
public static class ReadPublicTables
{
    public sealed record Standings(IReadOnlyList<ReadStandings.Response> Categories);

    public sealed record Leaders(IReadOnlyList<ReadLeaders.Response> Categories);

    public static IEndpointRouteBuilder MapReadPublicTables(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/standings", StandingsAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicTables))
            .WithSummary("Reads the standings of every category of a published competition.");

        routes.MapGet($"{PublicRoutes.Prefix}/leaders", LeadersAsync)
            .AsPublicReading()
            .WithName("ReadPublicLeaders")
            .WithSummary("Reads the leaderboards of a published competition.");

        return routes;
    }

    /// <summary>
    /// Every category's table, in one reading.
    /// </summary>
    /// <remarks>
    /// All of them together rather than one address per category, because a
    /// public page shows the divisions as tabs and asking per tab would make
    /// the first paint depend on how many divisions a league happens to run.
    /// </remarks>
    private static Task<IResult> StandingsAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        CancellationToken cancellationToken) =>
        PublishedAsync(
            organizationSlug,
            competitionSlug,
            reader,
            shows => shows.ShowStandings,
            async (database, categories) =>
            {
                var tables = new List<ReadStandings.Response>(categories.Count);

                foreach (var categoryId in categories)
                {
                    if (await StandingsQuery.ForCategoryAsync(database, categoryId, cancellationToken)
                        is { } table)
                    {
                        tables.Add(ReadStandings.Present(table));
                    }
                }

                return (object)new Standings(tables);
            },
            cancellationToken);

    private static Task<IResult> LeadersAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        CancellationToken cancellationToken,
        int top = 10) =>
        PublishedAsync(
            organizationSlug,
            competitionSlug,
            reader,
            shows => shows.ShowLeaders,
            async (database, categories) =>
            {
                // Clamped rather than refused. A visitor is not filling in a
                // form, they followed a link, and a page that answers a silly
                // number with an error is worse than one that answers it with
                // ten.
                var places = Math.Clamp(top, 1, 100);

                var boards = new List<ReadLeaders.Response>(categories.Count);

                foreach (var categoryId in categories)
                {
                    if (await LeadersQuery.ForCategoryAsync(
                            database, categoryId, places, cancellationToken) is { } ranked)
                    {
                        boards.Add(ReadLeaders.Present(ranked));
                    }
                }

                return (object)new Leaders(boards);
            },
            cancellationToken);

    /// <summary>
    /// Carries "the section is not published" back through a reader whose own
    /// null already means "the address does not resolve".
    /// </summary>
    /// <remarks>
    /// Both end as the same 404, so nothing is lost by telling them apart
    /// here rather than in the shared reader — and the reader keeps its null
    /// meaning exactly one thing, which is what makes it readable.
    /// </remarks>
    private sealed record Gate(object? Payload);

    /// <summary>
    /// Resolves the address, checks the section is published, and reads.
    /// </summary>
    /// <remarks>
    /// The gate and the resolution answer the same way: not found. Giving the
    /// two failures different shapes is exactly what an address-guesser is
    /// looking for — the difference between "no such competition" and "that
    /// competition keeps its table private".
    /// </remarks>
    private static async Task<IResult> PublishedAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        Func<Infrastructure.Persistence.Entities.PublicSettings, bool> published,
        Func<SportFrogDbContext, IReadOnlyList<Guid>, Task<object>> read,
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

                // No settings at all means the defaults the record declares,
                // which publish the table and the boards.
                if (!published(settings.Public ?? new Infrastructure.Persistence.Entities.PublicSettings()))
                {
                    return new Gate(null);
                }

                var categories = await database.Categories
                    .AsNoTracking()
                    .Where(category => category.CompetitionId == resolved.CompetitionId)
                    .OrderBy(category => category.DisplayOrder)
                    .ThenBy(category => category.Name)
                    .Select(category => category.Id)
                    .ToListAsync(cancellationToken);

                return new Gate(await read(database, categories));
            },
            cancellationToken);

        return page?.Payload is null ? Results.NotFound() : Results.Ok(page.Payload);
    }
}
