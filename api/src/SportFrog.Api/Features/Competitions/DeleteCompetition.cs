using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Withdraws a competition of the active organization.
/// </summary>
/// <remarks>
/// Logical, like clubs and athletes and unlike rulesets. A ruleset is a
/// setting and one that was never used leaves nothing behind; a competition
/// that was played is a piece of history that other rows name — an issued
/// credential says which competition it was issued for, and that has to keep
/// resolving after the season ends.
///
/// The address is freed by the deletion, matching the partial unique index:
/// next year's edition can take it.
/// </remarks>
public static class DeleteCompetition
{
    public static IEndpointRouteBuilder MapDeleteCompetition(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/competitions/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(DeleteCompetition))
            .WithSummary("Withdraws a competition that was never played.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        if (competition.Status is not (CompetitionState.Draft or CompetitionState.Cancelled))
        {
            // A competition that was announced, played or completed is not
            // removed but cancelled, and cancelling is a transition with its
            // own rules. Hiding a finished season would take its standings and
            // its issued credentials with it, which is a different act than
            // discarding a draft nobody saw.
            return Results.Problem(
                detail: "Only a competition still in draft, or one already cancelled, can be " +
                        "withdrawn. Cancel this one instead: what was played stays on record.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Set here rather than by the database, so the value comes from the
        // same clock the rest of the application is tested against.
        competition.DeletedAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
