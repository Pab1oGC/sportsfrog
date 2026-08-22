using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// The page a competition resolves to, for anybody with the address.
/// </summary>
/// <remarks>
/// The first thing that ever calls <c>resolve_public_competition</c>. The
/// address is two readable segments — the organization's and the
/// competition's — and never an identifier: a public URL that carried one
/// would invite walking the others.
///
/// Everything the reader hands back has already passed three checks made by
/// the database: the organization exists and is active, the competition
/// belongs to it, and it is published. An address failing any of them is
/// answered as not found, and the three are indistinguishable from outside
/// on purpose (RNF-16) — knowing a slug is not authorization to learn whether
/// an organization is suspended.
/// </remarks>
public static class ReadPublicCompetition
{
    /// <param name="Shows">
    /// Which sections this competition publishes. Returned so a page knows
    /// which tabs to draw rather than discovering it by asking for each one
    /// and being refused.
    /// </param>
    public sealed record Response(
        string OrganizationName,
        string Name,
        string Season,
        string SportCode,
        string SportName,
        string Format,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        Sections Shows,
        IReadOnlyList<CategorySummary> Categories);

    public sealed record Sections(bool Standings, bool Leaders, bool Rosters);

    public sealed record CategorySummary(
        Guid Id,
        string Name,
        string? Gender,
        int TeamCount);

    public static IEndpointRouteBuilder MapReadPublicCompetition(
        this IEndpointRouteBuilder routes)
    {
        routes.MapGet(PublicRoutes.Prefix, HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCompetition))
            .WithSummary("Reads a published competition by its public address.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        CancellationToken cancellationToken)
    {
        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                var competition = await database.Competitions
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == resolved.CompetitionId)
                    .Select(candidate => new
                    {
                        candidate.Name,
                        candidate.Season,
                        candidate.SportCode,
                        SportName = candidate.Sport!.Name,
                        candidate.Format,
                        candidate.Status,
                        candidate.StartsOn,
                        candidate.EndsOn,
                        candidate.Settings,
                        OrganizationName = database.Organizations
                            .Where(org => org.Id == resolved.OrganizationId)
                            .Select(org => org.Name)
                            .First(),
                    })

                    // Single rather than SingleOrDefault: the resolver found
                    // this row a moment ago, under this same context and this
                    // same transaction. Absent here would not be a missing
                    // page, it would be the isolation context having come
                    // undone between two statements — which is worth throwing
                    // over rather than answering with a tidy 404.
                    .SingleAsync(cancellationToken);

                var categories = await database.Categories
                    .AsNoTracking()
                    .Where(category => category.CompetitionId == resolved.CompetitionId)
                    .OrderBy(category => category.DisplayOrder)
                    .ThenBy(category => category.Name)
                    .Select(category => new CategorySummary(
                        category.Id,
                        category.Name,
                        category.Gender,
                        database.Teams.Count(team => team.CategoryId == category.Id)))
                    .ToListAsync(cancellationToken);

                var shows = competition.Settings.Public;

                return new Response(
                    competition.OrganizationName,
                    competition.Name,
                    competition.Season,
                    competition.SportCode,
                    competition.SportName,
                    competition.Format,
                    competition.Status,
                    competition.StartsOn,
                    competition.EndsOn,

                    // Absent settings mean the defaults the record declares:
                    // standings and leaders shown, rosters not. A competition
                    // published without ever opening its settings still has a
                    // page worth reading, and its rosters still stay private.
                    new Sections(
                        shows?.ShowStandings ?? true,
                        shows?.ShowLeaders ?? true,
                        shows?.ShowRosters ?? false),
                    categories);
            },
            cancellationToken);

        // One answer for every way of not being readable: wrong organization,
        // wrong competition, suspended, unpublished. Distinguishing them here
        // would hand an address-guesser a map.
        return page is null ? Results.NotFound() : Results.Ok(page);
    }
}
