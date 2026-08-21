using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Strikes an entry that should never have been made.
/// </summary>
/// <remarks>
/// This is the administrative correction, not the sporting one. A club that
/// withdraws mid-season is deactivated and keeps its results; this is for the
/// entry that was a mistake — the wrong club, the wrong category — and it
/// only applies while nothing has been played.
///
/// Logical rather than physical, because roster entries reference the team
/// with ON DELETE RESTRICT and a squad may already have been registered
/// against it. The place freed in the category is real, though: the unique
/// index ignores struck entries, so the club can be entered again.
/// </remarks>
public static class DeleteTeam
{
    public static IEndpointRouteBuilder MapDeleteTeam(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/teams/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteTeam))
            .WithSummary("Strikes a team entry that has never played.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TeamUsage usage,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var team = await database.Teams.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        if (await usage.HasMatchesAsync(id, cancellationToken))
        {
            // The database would not have stopped this. A logical delete
            // satisfies the foreign key on matches while leaving fixtures
            // that name a team nobody can read, and a table built from them
            // would be missing a side.
            return Results.Problem(
                detail: "This team appears in a fixture, so its entry cannot be struck. " +
                        "Deactivate it instead: it stops being scheduled and keeps what it " +
                        "already played.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Set here rather than by the database, so the value comes from the
        // same clock the rest of the application is tested against.
        team.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
