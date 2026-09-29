using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Takes a fixture off the calendar that should never have been on it.
/// </summary>
/// <remarks>
/// The administrative removal, and it is not how a match gets called off. A
/// match that will not be played is cancelled or postponed — both are states,
/// both stay on the calendar, and the standings read them differently. This
/// is for the fixture drawn by mistake.
///
/// Logical, because player events reference the match with ON DELETE CASCADE:
/// a physical delete would take the statistics with it and say nothing.
/// </remarks>
public static class DeleteMatch
{
    public static IEndpointRouteBuilder MapDeleteMatch(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/matches/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteMatch))
            .WithSummary("Removes a fixture that was drawn by mistake.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var match = await database.Matches.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (match is null)
        {
            return Results.NotFound();
        }

        if (match.Status is not (MatchState.Scheduled or MatchState.Postponed
            or MatchState.Cancelled))
        {
            // It was played, or awarded. Removing it would take a result out
            // of a table that was computed with it, and the two teams would
            // silently lose a match each.
            return Results.Problem(
                detail: "Este partido tiene un resultado, así que no se puede eliminar. Cancelálo " +
                        "en su lugar si no debería contar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Set here rather than by the database, so the value comes from the
        // same clock the rest of the application is tested against.
        match.DeletedAt = clock.GetUtcNow();

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody recorded a result, rescheduled it, or deleted it
            // themselves between the check above and this save.
            return Results.Problem(
                detail: "Alguien más cambió este partido mientras vos lo tenías abierto. Volvé a " +
                        "leerlo antes de eliminarlo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
