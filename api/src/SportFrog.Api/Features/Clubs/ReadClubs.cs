using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Clubs;

/// <summary>
/// Lists the clubs of the active organization, or reads one.
///
/// Neither query filters by organization: the isolation context already
/// restricts what the database will return, and repeating the filter here
/// would suggest the guarantee lives in this code.
/// </summary>
public static class ReadClubs
{
    /// <param name="LogoUrl">
    /// A temporary link to the crest, not the crest and not its permanent
    /// address. It expires; it is meant to be loaded now, by the page that
    /// asked for it, and not stored anywhere.
    /// </param>
    public sealed record Summary(Guid Id, string Name, string? ShortName, string? LogoUrl, bool IsActive);

    public static IEndpointRouteBuilder MapReadClubs(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/clubs", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadClubs))
            .WithSummary("Lists the clubs of the active organization.");

        routes.MapGet("/clubs/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadClub")
            .WithSummary("Reads one club.");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        ClubPhoto photos,
        CancellationToken cancellationToken,
        string? search = null)
    {
        search = QueryFilter.OrAbsent(search);

        var clubs = await database.Clubs
            // Not a club a delegate manages, so it has no place in a list or
            // a picker built for those — see UnaffiliatedClub.
            .Where(club => !club.IsUnaffiliated)
            .Where(club => search == null || EF.Functions.ILike(club.Name, $"%{search}%"))
            .OrderBy(club => club.Name)
            .ToListAsync(cancellationToken);

        var listing = new List<Summary>(clubs.Count);

        foreach (var club in clubs)
        {
            listing.Add(await PresentAsync(club, photos, cancellationToken));
        }

        return Results.Ok(listing);
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        ClubPhoto photos,
        CancellationToken cancellationToken)
    {
        var club = await database.Clubs.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        // A club of another organization is not found rather than forbidden,
        // and that is not a choice made here: the policy never returned it, so
        // there is nothing to distinguish it from one that does not exist.
        return club is null ? Results.NotFound() : Results.Ok(await PresentAsync(club, photos, cancellationToken));
    }

    /// <summary>Turns a row into what a reader gets, signing the crest on the way.</summary>
    private static async Task<Summary> PresentAsync(
        Club club,
        ClubPhoto photos,
        CancellationToken cancellationToken) =>
        new(
            club.Id,
            club.Name,
            club.ShortName,
            await photos.LinkAsync(club.LogoUrl, cancellationToken),
            club.IsActive);
}
