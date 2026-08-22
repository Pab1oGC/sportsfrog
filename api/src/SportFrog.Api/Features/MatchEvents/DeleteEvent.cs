using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

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
            .WithName(nameof(DeleteEvent))
            .WithSummary("Removes an event recorded by mistake.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var recorded = await database.PlayerEvents.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (recorded is null)
        {
            return Results.NotFound();
        }

        database.PlayerEvents.Remove(recorded);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
