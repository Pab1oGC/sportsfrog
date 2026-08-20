using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// Removes a person from the register.
///
/// Logically, always. Roster entries reference athletes with ON DELETE
/// RESTRICT, and every event ever recorded is attributed through them: erasing
/// the row would erase somebody's record of having played.
///
/// This is an administrative correction of a mistaken registration. Someone
/// who stopped competing is deactivated instead, and someone who left a team
/// mid-season is withdrawn from its roster — three different facts that the
/// schema keeps apart on purpose.
/// </summary>
public static class DeleteAthlete
{
    public static IEndpointRouteBuilder MapDeleteAthlete(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/athletes/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteAthlete))
            .WithSummary("Removes a person from the register.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var athlete = await database.Athletes.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (athlete is null)
        {
            return Results.NotFound();
        }

        athlete.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
