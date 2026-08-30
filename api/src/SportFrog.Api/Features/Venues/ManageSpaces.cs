using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Venues;

/// <summary>
/// The pitches, courts and fields a venue is made of — the things a fixture
/// is actually placed on.
/// </summary>
public static class ManageSpaces
{
    public sealed record Request(string Name, bool IsActive);

    public sealed record Response(Guid Id);

    /// <param name="IsAvailable">
    /// Whether a fixture can be placed here right now, which is the space
    /// being active <em>and</em> its venue being active. Answered here so
    /// every caller does not have to remember the second half.
    /// </param>
    public sealed record Summary(
        Guid Id,
        Guid VenueId,
        string VenueName,
        string Name,
        bool IsActive,
        bool IsAvailable);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre del espacio es obligatorio.")
                .MaximumLength(120);
        }
    }

    public static IEndpointRouteBuilder MapVenueSpaces(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/venues/{venueId:guid}/spaces", CreateAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("CreateVenueSpace")
            .WithSummary("Adds a space to a venue.");

        routes.MapGet("/venues/{venueId:guid}/spaces", ListForVenueAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadVenueSpaces")
            .WithSummary("Lists the spaces of a venue.");

        // Across every venue, which is the question the calendar asks: what
        // can this organization play on. Reaching it venue by venue would make
        // the scheduler assemble an answer the database can give in one go.
        routes.MapGet("/spaces", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadSpaces")
            .WithSummary("Lists every space of the organization.");

        routes.MapGet("/spaces/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadSpace")
            .WithSummary("Reads one space.");

        routes.MapPut("/spaces/{id:guid}", UpdateAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("UpdateSpace")
            .WithSummary("Corrects a space.");

        routes.MapDelete("/spaces/{id:guid}", DeleteAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName("DeleteSpace")
            .WithSummary("Removes a space nothing was ever played on.");

        return routes;
    }

    private static async Task<IResult> CreateAsync(
        Guid venueId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (!await database.Venues.AnyAsync(venue => venue.Id == venueId, cancellationToken))
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();

        if (await database.VenueSpaces.AnyAsync(
                space => space.VenueId == venueId && space.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Esta sede ya tiene un espacio con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var space = new VenueSpace
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            VenueId = venueId,
            Name = name,
            IsActive = request.IsActive,
        };

        database.VenueSpaces.Add(space);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Esta sede ya tiene un espacio con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/spaces/{space.Id}", new Response(space.Id));
    }

    private static async Task<IResult> ListForVenueAsync(
        Guid venueId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        // The venue is checked first: one that does not exist and one with no
        // spaces yet are different answers, and both would otherwise come back
        // as an empty list.
        if (!await database.Venues.AnyAsync(venue => venue.Id == venueId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.VenueSpaces
                .Where(space => space.VenueId == venueId)
                .OrderBy(space => space.Name))
            .ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Every space in the organization, optionally only the bookable ones.
    /// </summary>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        bool availableOnly = false) =>
        Results.Ok(await Project(database.VenueSpaces
                .Where(space => !availableOnly || (space.IsActive && space.Venue!.IsActive))
                .OrderBy(space => space.Venue!.Name)
                .ThenBy(space => space.Name))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var space = await Project(database.VenueSpaces.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return space is null ? Results.NotFound() : Results.Ok(space);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var space = await database.VenueSpaces.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (space is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();

        if (await database.VenueSpaces.AnyAsync(
                other => other.VenueId == space.VenueId && other.Id != id && other.Name == name,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Esta sede ya tiene un espacio con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        space.Name = name;
        space.IsActive = request.IsActive;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Esta sede ya tiene un espacio con ese nombre.",
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
        var space = await database.VenueSpaces.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (space is null)
        {
            return Results.NotFound();
        }

        if (await usage.IsSpaceInUseAsync(id, cancellationToken))
        {
            // The foreign key here is ON DELETE SET NULL, so the database
            // would have accepted this and quietly emptied the venue out of
            // every fixture placed on it. This check is the only thing
            // standing between a delete and a calendar with no locations.
            return Results.Problem(
                detail: "Este espacio está en uso — hay un partido colocado en él, o una " +
                        "competencia programa contra él — así que no se puede eliminar. " +
                        "Desactivalo en su lugar: deja de ofrecerse para partidos nuevos y todo " +
                        "lo que ya apunta a él sigue apuntando.",
                statusCode: StatusCodes.Status409Conflict);
        }

        database.VenueSpaces.Remove(space);

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IQueryable<Summary> Project(IQueryable<VenueSpace> spaces) =>
        spaces.Select(space => new Summary(
            space.Id,
            space.VenueId,
            space.Venue!.Name,
            space.Name,
            space.IsActive,
            space.IsActive && space.Venue.IsActive));
}
