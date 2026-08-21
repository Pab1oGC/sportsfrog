using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// Lists the people registered by the active organization, or reads one.
///
/// This is the administrative view, so it shows the document and the guardian
/// contact. None of that may reach the public view (RNF-16), which will need
/// its own shape rather than reusing this one.
/// </summary>
public static class ReadAthletes
{
    public sealed record Summary(
        Guid Id,
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        string? Gender,
        string? PhotoUrl,
        string? GuardianName,
        string? GuardianPhone,
        bool IsActive);

    public static IEndpointRouteBuilder MapReadAthletes(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/athletes", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadAthletes))
            .WithSummary("Lists the people registered by the active organization.");

        routes.MapGet("/athletes/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadAthlete")
            .WithSummary("Reads one person.");

        return routes;
    }

    /// <summary>
    /// Searches by name or by document.
    /// </summary>
    /// <remarks>
    /// Searching by document is what makes the register usable: it answers
    /// "is this person already here" before a second record is created for
    /// them, which is the same question RF-43 exists to settle.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? search = null)
    {
        search = QueryFilter.OrAbsent(search);

        return Results.Ok(await database.Athletes
            .Where(athlete => search == null
                || athlete.DocumentId == search
                || EF.Functions.ILike(athlete.LastName, $"%{search}%")
                || EF.Functions.ILike(athlete.FirstName, $"%{search}%"))
            .OrderBy(athlete => athlete.LastName)
            .ThenBy(athlete => athlete.FirstName)
            .Select(athlete => Project(athlete))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var athlete = await database.Athletes
            .Where(candidate => candidate.Id == id)
            .Select(candidate => Project(candidate))
            .SingleOrDefaultAsync(cancellationToken);

        return athlete is null ? Results.NotFound() : Results.Ok(athlete);
    }

    private static Summary Project(Athlete athlete) => new(
        athlete.Id,
        athlete.FirstName,
        athlete.LastName,
        athlete.DocumentId,
        athlete.BirthDate,
        athlete.Gender,
        athlete.PhotoUrl,
        athlete.GuardianName,
        athlete.GuardianPhone,
        athlete.IsActive);
}
