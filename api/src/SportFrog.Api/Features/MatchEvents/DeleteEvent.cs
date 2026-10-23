using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.MatchEvents;

/// <summary>
/// Removes an event that did not happen.
/// </summary>
/// <remarks>
/// Physical, and deliberately so. The table carries no deleted_at because an
/// event entered by mistake is not a fact that occurred and was undone — a
/// goal tapped against the wrong player never happened to that player, and
/// leaving a hidden row would keep it in the count of anything that forgets to
/// filter.
///
/// The audit log is what records that somebody removed it, which is the
/// question worth asking afterwards.
/// </remarks>
public static class DeleteEvent
{
    public static IEndpointRouteBuilder MapDeleteEvent(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/events/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Recorder)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.PlayerEvent, "id"))
            .WithName(nameof(DeleteEvent))
            .WithSummary("Removes an event recorded by mistake.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        EventDrivenResult resync,
        CancellationToken cancellationToken)
    {
        var recorded = await database.PlayerEvents.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (recorded is null)
        {
            return Results.NotFound();
        }

        var matchId = recorded.MatchId;

        database.PlayerEvents.Remove(recorded);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else corrected or deleted this same event in the
            // meantime — deleting a row that is already gone is not treated
            // as success.
            return Results.Problem(
                detail: "Alguien más corrigió o eliminó este evento mientras vos lo tenías abierto. " +
                        "Volvé a leer los eventos del partido antes de eliminarlo de nuevo.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Best-effort, and after the delete is already saved — see
        // EventDrivenResult's own remarks for when it does nothing instead.
        await resync.ResyncAsync(matchId, database, cancellationToken);

        return Results.NoContent();
    }
}
