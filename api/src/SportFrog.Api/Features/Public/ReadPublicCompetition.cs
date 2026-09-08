using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
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

        /// <summary>The organization's own mark, wherever it runs competitions.</summary>
        string? OrganizationLogoUrl,
        string Name,
        string Season,
        string SportCode,
        string SportName,

        /// <summary>What one unit of score is called: "gol", "punto".</summary>
        string ScoringUnit,

        /// <summary>What one division of a match is called: "tiempo", "set".</summary>
        string PeriodLabel,

        /// <summary>
        /// Whether this sport is decided by periods won rather than a total
        /// score, so a standings column or a tiebreaker label never has to
        /// guess it from the sport's name. See
        /// <see cref="Rulebook.ReadSports.Summary"/>, which exposes the same
        /// fact to the organization's own pages.
        /// </summary>
        bool IsPlayedInSets,

        /// <summary>
        /// Whether this sport is decided by a score judges hand down rather
        /// than a table — poomsae, not yet any other sport in the catalog.
        /// The other branch a public page needs alongside
        /// <see cref="IsPlayedInSets"/>: a judged category has a
        /// classification to rank, not a standings table to show.
        /// </summary>
        bool IsJudged,

        string Format,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        Sections Shows,
        IReadOnlyList<CategorySummary> Categories,
        Portal Portal);

    public sealed record Sections(bool Standings, bool Leaders, bool Rosters, bool Classification);

    public sealed record CategorySummary(
        Guid Id,
        string Name,
        string? Gender,
        int TeamCount);

    /// <summary>
    /// However this competition dressed up its own page, on top of the plain
    /// one every competition gets. Every field is absent unless somebody set
    /// it, which is what keeps a page that never opened these settings
    /// looking exactly as it always did.
    /// </summary>
    public sealed record Portal(
        string? BannerUrl,
        string? AccentColor,
        string? Description,
        string? Instagram,
        string? Facebook,
        string? WhatsApp,
        string? Website,
        IReadOnlyList<SponsorSummary> Sponsors);

    public sealed record SponsorSummary(string? Name, string? Url, string LogoUrl);

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
        ObjectStore store,
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
                        ScoringUnit = candidate.Sport.ScoringUnit,
                        PeriodLabel = candidate.Sport.PeriodLabel,
                        IsPlayedInSets = candidate.Sport.ScoreMode == ScoreMode.Sets,
                        IsJudged = candidate.Sport.ScoreMode == ScoreMode.Judged,
                        candidate.Format,
                        candidate.Status,
                        candidate.StartsOn,
                        candidate.EndsOn,
                        candidate.Settings,
                        Organization = database.Organizations
                            .Where(org => org.Id == resolved.OrganizationId)
                            .Select(org => new { org.Name, org.LogoUrl })
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

                var organizationLogoUrl = string.IsNullOrEmpty(competition.Organization.LogoUrl)
                    ? null
                    : await store.ReadLinkAsync(
                        resolved.OrganizationId, competition.Organization.LogoUrl, cancellationToken);

                var bannerUrl = string.IsNullOrEmpty(shows?.BannerKey)
                    ? null
                    : await store.ReadLinkAsync(resolved.OrganizationId, shows.BannerKey, cancellationToken);

                var sponsors = new List<SponsorSummary>();

                foreach (var sponsor in shows?.Sponsors ?? [])
                {
                    if (await store.ReadLinkAsync(resolved.OrganizationId, sponsor.LogoKey, cancellationToken)
                        is { } logoUrl)
                    {
                        sponsors.Add(new SponsorSummary(sponsor.Name, sponsor.Url, logoUrl));
                    }
                }

                return new Response(
                    competition.Organization.Name,
                    organizationLogoUrl,
                    competition.Name,
                    competition.Season,
                    competition.SportCode,
                    competition.SportName,
                    competition.ScoringUnit,
                    competition.PeriodLabel,
                    competition.IsPlayedInSets,
                    competition.IsJudged,
                    competition.Format,
                    competition.Status,
                    competition.StartsOn,
                    competition.EndsOn,

                    // Absent settings mean the defaults the record declares:
                    // standings, leaders and classification shown, rosters
                    // not. A competition published without ever opening its
                    // settings still has a page worth reading, and its
                    // rosters still stay private.
                    new Sections(
                        shows?.ShowStandings ?? true,
                        shows?.ShowLeaders ?? true,
                        shows?.ShowRosters ?? false,
                        shows?.ShowClassification ?? true),
                    categories,
                    new Portal(
                        bannerUrl,
                        shows?.AccentColor,
                        shows?.Description,
                        shows?.Instagram,
                        shows?.Facebook,
                        shows?.WhatsApp,
                        shows?.Website,
                        sponsors));
            },
            cancellationToken);

        // One answer for every way of not being readable: wrong organization,
        // wrong competition, suspended, unpublished. Distinguishing them here
        // would hand an address-guesser a map.
        return page is null ? Results.NotFound() : Results.Ok(page);
    }
}
