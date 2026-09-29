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
    /// Searches by name or by document, and optionally narrows by gender, by
    /// an age range, and by a weight range.
    /// </summary>
    /// <remarks>
    /// Searching by document is what makes the register usable: it answers
    /// "is this person already here" before a second record is created for
    /// them, which is the same question RF-43 exists to settle — and, same as
    /// a name, an operator dialing in a document from memory rarely has every
    /// digit of it, so this matches partially too, not only a full document
    /// typed exactly. The one place a document genuinely has to be exact — the
    /// duplicate check when a new person is registered — is a question of its
    /// own, answered by <see cref="Athletes.CreateAthlete"/> directly against
    /// the database, not by filtering this listing.
    ///
    /// Gender, age and weight moved here from the panel's own filtering,
    /// which used to run in the browser over whatever this endpoint already
    /// handed it — correct only as long as that was every athlete in the
    /// organization. <c>minAge</c>/<c>maxAge</c> translate to a birth date
    /// range the same way the panel's own <c>edad()</c> counts a birthday —
    /// exact years, one already had — not to a plain year-of-birth
    /// subtraction, and an athlete with no birth date or no weight on file
    /// is excluded by an active range rather than guessed into either side
    /// of it, the same rule the panel's filter already followed.
    /// </remarks>
    // internal, not private: testable directly against a real database, the
    // same convention CreateCompetition.HandleAsync already uses — the
    // age-range boundary math below is exactly the kind of off-by-one that
    // a test needs to pin, not just read.
    internal static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        AthletePhoto photos,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken cancellationToken,
        string? search = null,
        string? gender = null,
        int? minAge = null,
        int? maxAge = null,
        decimal? minWeight = null,
        decimal? maxWeight = null,
        int? skip = null,
        int? take = null)
    {
        search = QueryFilter.OrAbsent(search);
        gender = QueryFilter.OrAbsent(gender);

        var query = database.Athletes
            .Where(athlete => search == null
                || EF.Functions.ILike(athlete.DocumentId, $"%{search}%")
                || EF.Functions.ILike(athlete.LastName, $"%{search}%")
                || EF.Functions.ILike(athlete.FirstName, $"%{search}%"))
            .Where(athlete => gender == null || athlete.Gender == gender);

        if (minAge is not null || maxAge is not null)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

            // Age A is every birth date strictly after today minus (A+1)
            // years and no later than today minus A years — the same
            // one-year window edad() counts by comparing month and day, not
            // by subtracting calendar years. A birth date right on either
            // boundary is handled the same way edad() itself would count it.
            if (maxAge is { } max)
            {
                query = query.Where(athlete => athlete.BirthDate > today.AddYears(-(max + 1)));
            }

            if (minAge is { } min)
            {
                query = query.Where(athlete => athlete.BirthDate <= today.AddYears(-min));
            }
        }

        if (minWeight is not null || maxWeight is not null)
        {
            query = query.Where(athlete => athlete.WeightKg != null
                && (minWeight == null || athlete.WeightKg >= minWeight)
                && (maxWeight == null || athlete.WeightKg <= maxWeight));
        }

        query = query
            .OrderBy(athlete => athlete.LastName)
            .ThenBy(athlete => athlete.FirstName);

        var athletes = await PagedListing.ApplyAsync(query, httpContext, skip, take, cancellationToken);

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
