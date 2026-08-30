using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Strikes a registration that should never have been made.
/// </summary>
/// <remarks>
/// The administrative removal, and the rarer of the two. It is for the wrong
/// person entered on the wrong team — not for a player who left, which is a
/// withdrawal and keeps its record.
///
/// Refused once anything has been recorded against the entry, because at that
/// point the registration is not a mistake: somebody scored those goals. The
/// distinction matters enough that the schema keeps two columns for it, and
/// this endpoint is the half that has to be argued for.
///
/// Striking frees the person to be registered again — for this team or for
/// another in the same category — because the unique indexes ignore struck
/// rows. That is the point of it.
/// </remarks>
public static class StrikeRegistration
{
    public static IEndpointRouteBuilder MapStrikeRegistration(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/roster/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(StrikeRegistration))
            .WithSummary("Strikes a registration that was entered by mistake.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        RosterUsage usage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var entry = await database.RosterEntries.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        if (await usage.HasRecordedEventsAsync(id, cancellationToken))
        {
            // The database would not have stopped this: striking is a logical
            // delete, so the foreign key on player events stays satisfied by a
            // row nobody can read, and the statistics would go on counting
            // goals for a player who was never registered as far as any query
            // can tell.
            return Results.Problem(
                detail: "Se registraron eventos para este jugador, así que el registro no se " +
                        "puede anular. Retiralo en su lugar: deja la nómina y lo que hizo queda " +
                        "en el registro.",
                statusCode: StatusCodes.Status409Conflict);
        }

        entry.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
