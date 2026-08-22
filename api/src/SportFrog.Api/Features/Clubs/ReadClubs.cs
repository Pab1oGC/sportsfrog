using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

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
        CancellationToken cancellationToken,
        string? search = null) =>
        Results.Ok(await database.Clubs
            .Where(club => search == null || EF.Functions.ILike(club.Name, $"%{search}%"))
            .OrderBy(club => club.Name)
            .Select(club => new Summary(
                club.Id, club.Name, club.ShortName, club.LogoUrl, club.IsActive))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var club = await database.Clubs
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new Summary(
                candidate.Id, candidate.Name, candidate.ShortName, candidate.LogoUrl, candidate.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        // A club of another organization is not found rather than forbidden,
        // and that is not a choice made here: the policy never returned it, so
        // there is nothing to distinguish it from one that does not exist.
        return club is null ? Results.NotFound() : Results.Ok(club);
    }
}
