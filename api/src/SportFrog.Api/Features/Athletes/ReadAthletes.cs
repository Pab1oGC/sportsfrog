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
    /// <param name="PhotoUrl">
    /// A temporary link to the photograph, not the photograph and not its
    /// permanent address. It expires; it is meant to be loaded now, by the
    /// page that asked for it, and not stored anywhere.
    /// </param>
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
        bool IsActive,
        decimal? WeightKg);

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
        AthletePhoto photos,
        CancellationToken cancellationToken,
        string? search = null)
    {
        search = QueryFilter.OrAbsent(search);

        var athletes = await database.Athletes
            .Where(athlete => search == null
                || athlete.DocumentId == search
                || EF.Functions.ILike(athlete.LastName, $"%{search}%")
                || EF.Functions.ILike(athlete.FirstName, $"%{search}%"))
            .OrderBy(athlete => athlete.LastName)
            .ThenBy(athlete => athlete.FirstName)
            .ToListAsync(cancellationToken);

        var listing = new List<Summary>(athletes.Count);

        foreach (var athlete in athletes)
        {
            listing.Add(await PresentAsync(athlete, photos, cancellationToken));
        }

        return Results.Ok(listing);
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        AthletePhoto photos,
        CancellationToken cancellationToken)
    {
        var athlete = await database.Athletes.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        return athlete is null
            ? Results.NotFound()
            : Results.Ok(await PresentAsync(athlete, photos, cancellationToken));
    }

    /// <summary>
    /// Turns a row into what a reader gets, signing the photograph on the way.
    /// </summary>
    /// <remarks>
    /// Signing is arithmetic, not a round trip: no call leaves the process,
    /// so a listing of several hundred people costs several hundred HMACs and
    /// nothing else. Worth stating, because a loop that looks like this
    /// usually is the problem.
    /// </remarks>
    private static async Task<Summary> PresentAsync(
        Athlete athlete,
        AthletePhoto photos,
        CancellationToken cancellationToken) =>
        new(
            athlete.Id,
            athlete.FirstName,
            athlete.LastName,
            athlete.DocumentId,
            athlete.BirthDate,
            athlete.Gender,
            await photos.LinkAsync(athlete.PhotoKey, cancellationToken),
            athlete.GuardianName,
            athlete.GuardianPhone,
            athlete.IsActive,
            athlete.WeightKg);
}
