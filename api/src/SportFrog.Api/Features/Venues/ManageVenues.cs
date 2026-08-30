using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Venues;

/// <summary>
/// The places an organization plays at.
/// </summary>
/// <remarks>
/// One slice for all four operations, unlike the larger modules. A venue is a
/// name, an address and a flag; splitting that across four files would spread
/// forty lines over four headers and put the delete rule — the only part with
/// anything to say — where it is hardest to find.
/// </remarks>
public static class ManageVenues
{
    public sealed record Request(string Name, string? Address, bool IsActive);

    public sealed record Response(Guid Id);

    public sealed record Summary(
        Guid Id,
        string Name,
        string? Address,
        bool IsActive,
        int SpaceCount);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre de la sede es obligatorio.")
                .MaximumLength(120);

            RuleFor(request => request.Address)
                .MaximumLength(250)
                .When(request => request.Address is not null);
        }
    }

    public static IEndpointRouteBuilder MapVenues(this IEndpointRouteBuilder routes)
    {
        // Configuration of the organization, like its rulesets: set up once
        // and rarely touched. Whoever runs a fixture chooses among the venues;
        // deciding which exist is the administrator's.
        routes.MapPost("/venues", CreateAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("CreateVenue")
            .WithSummary("Registers a venue.");

        routes.MapGet("/venues", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadVenues")
            .WithSummary("Lists the venues of the active organization.");

        routes.MapGet("/venues/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadVenue")
            .WithSummary("Reads one venue.");

        routes.MapPut("/venues/{id:guid}", UpdateAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("UpdateVenue")
            .WithSummary("Corrects a venue.");

        routes.MapDelete("/venues/{id:guid}", DeleteAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("DeleteVenue")
            .WithSummary("Removes a venue nothing was ever played at.");

        return routes;
    }

    private static async Task<IResult> CreateAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await database.Venues.AnyAsync(venue => venue.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe una sede con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            Name = name,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            IsActive = request.IsActive,
        };

        database.Venues.Add(venue);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Ya existe una sede con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/venues/{venue.Id}", new Response(venue.Id));
    }

    /// <summary>
    /// The venues, with how many spaces each has.
    /// </summary>
    /// <remarks>
    /// The count comes along because a venue with no spaces cannot host
    /// anything, and that is invisible from its name — an organizer wondering
    /// why a ground never appears when placing a fixture is looking at a zero
    /// here.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        string? search = null,
        bool activeOnly = false)
    {
        search = QueryFilter.OrAbsent(search);

        return Results.Ok(await Project(database.Venues
                .Where(venue => search == null || EF.Functions.ILike(venue.Name, $"%{search}%"))
                .Where(venue => !activeOnly || venue.IsActive)
                .OrderBy(venue => venue.Name))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var venue = await Project(database.Venues.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return venue is null ? Results.NotFound() : Results.Ok(venue);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var venue = await database.Venues.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (venue is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();

        if (await database.Venues.AnyAsync(
                other => other.Id != id && other.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe una sede con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        venue.Name = name;
        venue.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        // Deactivating is not removing: the ground stops being offered for new
        // fixtures, its spaces stop with it, and everything played there keeps
        // pointing at it.
        venue.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Ya existe una sede con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        SportFrogDbContext database,
        VenueUsage usage,
        CancellationToken cancellationToken)
    {
        var venue = await database.Venues.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (venue is null)
        {
            return Results.NotFound();
        }

        if (await usage.IsVenueInUseAsync(id, cancellationToken))
        {
            // Nothing would have stopped this. Removing the venue cascades to
            // its spaces, and each of those empties itself out of every
            // fixture placed on it — no error, no trace, a calendar that has
            // quietly forgotten where it is being played.
            return Results.Problem(
                detail: "Esta sede está en uso — hay un partido colocado en uno de sus espacios, " +
                        "o una competencia programa contra uno — así que no se puede eliminar. " +
                        "Desactivala en su lugar: deja de ofrecerse para partidos nuevos y todo " +
                        "lo que ya apunta a ella sigue apuntando.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Physical, and it takes the spaces with it. That is the schema's
        // cascade doing what it is for: a venue that was never used has
        // nothing worth keeping, and its pitches have no meaning without it.
        database.Venues.Remove(venue);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IQueryable<Summary> Project(IQueryable<Venue> venues) =>
        venues.Select(venue => new Summary(
            venue.Id,
            venue.Name,
            venue.Address,
            venue.IsActive,
            venue.Spaces.Count));
}
