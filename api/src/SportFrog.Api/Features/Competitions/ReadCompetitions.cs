using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Lists the competitions of the active organization, or reads one.
///
/// Neither query filters by organization: the isolation context already
/// restricts what the database will return, and repeating the filter here
/// would suggest the guarantee lives in this code.
/// </summary>
public static class ReadCompetitions
{
    public sealed record Summary(
        Guid Id,
        string SportCode,
        Guid RulesetId,
        string RulesetName,
        string Name,
        string Slug,
        string Season,
        string Format,
        CaptureLevel CaptureLevel,
        CompetitionState Status,
        DateOnly? StartsOn,
        DateOnly? EndsOn,
        bool IsPublic,
        CompetitionSettings Settings,
        int CategoryCount,
        PublicPreview? PublicPreview);

    /// <summary>
    /// Temporary links for whatever pictures the competition's public
    /// settings reference, so the admin panel can show them without signing
    /// a key itself. Kept apart from <see cref="Summary.Settings"/> rather
    /// than written into it, because that field keeps the raw keys — what a
    /// save sends back unchanged when a picture was left alone.
    /// </summary>
    public sealed record PublicPreview(
        string? BannerUrl, string? LogoUrl, IReadOnlyList<SponsorPreview> Sponsors, IReadOnlyList<GalleryPreview> Gallery);

    /// <param name="LogoKey">
    /// The same key <see cref="Summary.Settings"/> carries for this sponsor,
    /// repeated here so a caller can match a preview back to its entry
    /// without depending on both lists staying in the same order.
    /// </param>
    public sealed record SponsorPreview(string LogoKey, string? Name, string? Url, string LogoUrl);

    /// <param name="Key">Same reason as <see cref="SponsorPreview.LogoKey"/> — matches a preview back to its stored entry.</param>
    public sealed record GalleryPreview(string Key, string? Caption, string Url);

    public static IEndpointRouteBuilder MapReadCompetitions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCompetitions))
            .WithSummary("Lists the competitions of the active organization.");

        routes.MapGet("/competitions/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCompetition")
            .WithSummary("Reads one competition.");

        return routes;
    }

    /// <summary>
    /// The organization's competitions, newest season first.
    /// </summary>
    /// <remarks>
    /// The status filter is what makes this list usable after a few seasons:
    /// the question is almost always "what is running now", and an
    /// organization that has been on the platform for three years has far
    /// more finished competitions than live ones.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        PortalPicture pictures,
        CancellationToken cancellationToken,
        string? status = null,
        string? sport = null,
        string? search = null)
    {
        // A query parameter reaches no validator — the contract filter only
        // sees arguments that have one — so an unrecognized state is answered
        // here, and answered the same way a bad field in a body would be.
        // Bound as an enum it would come back as a bare 400 with no body at
        // all, which tells the caller nothing about which of three parameters
        // it disliked.
        CompetitionState? state = null;

        sport = QueryFilter.OrAbsent(sport);
        search = QueryFilter.OrAbsent(search);

        if (QueryFilter.OrAbsent(status) is { } requested)
        {
            if (!WireEnum.TryParse<CompetitionState>(requested, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] =
                    [
                        $"Unknown state. Available: {WireEnum.Options<CompetitionState>()}.",
                    ],
                });
            }

            state = parsed;
        }

        var competitions = await Project(database, database.Competitions
                .Where(competition => state == null || competition.Status == state)
                .Where(competition => sport == null || competition.SportCode == sport)
                .Where(competition => search == null
                    || EF.Functions.ILike(competition.Name, $"%{search}%"))
                .OrderByDescending(competition => competition.Season)
                .ThenBy(competition => competition.Name))
            .ToListAsync(cancellationToken);

        var listing = new List<Summary>(competitions.Count);

        foreach (var competition in competitions)
        {
            listing.Add(await PresentAsync(competition, pictures, cancellationToken));
        }

        return Results.Ok(listing);
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var competition = await Project(database, database.Competitions.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return competition is null
            ? Results.NotFound()
            : Results.Ok(await PresentAsync(competition, pictures, cancellationToken));
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same competition differently.
    /// </summary>
    /// <remarks>
    /// The ruleset's name travels alongside its identifier because that is
    /// what a competition is recognized by in an interface. Reading it here
    /// costs a join the database was going to make anyway, and saves the
    /// caller a second request for a single string.
    ///
    /// This stops short of the final shape: it still carries the settings'
    /// raw picture keys rather than signed links, because signing needs
    /// <see cref="PortalPicture"/> and cannot happen inside a query the
    /// database is asked to translate. <see cref="PresentAsync"/> finishes
    /// the job once this has run.
    /// </remarks>
    private static IQueryable<Summary> Project(SportFrogDbContext database, IQueryable<Competition> competitions) =>
        competitions.Select(competition => new Summary(
            competition.Id,
            competition.SportCode,
            competition.RulesetId,
            competition.Ruleset!.Name,
            competition.Name,
            competition.Slug,
            competition.Season,
            competition.Format,
            competition.CaptureLevel,
            competition.Status,
            competition.StartsOn,
            competition.EndsOn,
            competition.IsPublic,
            competition.Settings,
            database.Categories.Count(category => category.CompetitionId == competition.Id),
            null));

    /// <summary>Signs whatever pictures the competition's public settings reference.</summary>
    private static async Task<Summary> PresentAsync(
        Summary competition, PortalPicture pictures, CancellationToken cancellationToken)
    {
        if (competition.Settings.Public is not { } @public)
        {
            return competition;
        }

        var bannerUrl = @public.BannerKey is { } banner
            ? await pictures.LinkAsync(banner, cancellationToken)
            : null;

        var logoUrl = @public.LogoKey is { } logo
            ? await pictures.LinkAsync(logo, cancellationToken)
            : null;

        var sponsors = new List<SponsorPreview>();

        foreach (var sponsor in @public.Sponsors ?? [])
        {
            if (await pictures.LinkAsync(sponsor.LogoKey, cancellationToken) is { } sponsorLogoUrl)
            {
                sponsors.Add(new SponsorPreview(sponsor.LogoKey, sponsor.Name, sponsor.Url, sponsorLogoUrl));
            }
        }

        var gallery = new List<GalleryPreview>();

        foreach (var photo in @public.Gallery ?? [])
        {
            if (await pictures.LinkAsync(photo.Key, cancellationToken) is { } photoUrl)
            {
                gallery.Add(new GalleryPreview(photo.Key, photo.Caption, photoUrl));
            }
        }

        return competition with { PublicPreview = new PublicPreview(bannerUrl, logoUrl, sponsors, gallery) };
    }
}
