using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Clubs;

/// <summary>
/// Removes a club from the active organization.
///
/// Logically, always. Teams reference clubs with ON DELETE RESTRICT, so a
/// club that ever entered a category cannot be erased without taking results
/// with it — and the row is what those results are attributed to.
/// </summary>
public static class DeleteClub
{
    public static IEndpointRouteBuilder MapDeleteClub(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/clubs/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteClub))
            .WithSummary("Removes a club.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var club = await database.Clubs.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (club is null)
        {
            return Results.NotFound();
        }

        club.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
