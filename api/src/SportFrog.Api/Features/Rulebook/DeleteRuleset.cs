using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Removes a ruleset of the active organization.
/// </summary>
/// <remarks>
/// Physical, unlike clubs and athletes. Those are records of something that
/// exists in the world and keeps its history, so removing one hides it and
/// nothing else. A ruleset is a setting: one that was never used leaves
/// nothing behind, and one that was used cannot be removed at all.
/// </remarks>
public static class DeleteRuleset
{
    public static IEndpointRouteBuilder MapDeleteRuleset(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/rulesets/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(DeleteRuleset))
            .WithSummary("Removes a ruleset that has never been used.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        RulesetUsage usage,
        CancellationToken cancellationToken)
    {
        var ruleset = await database.Rulesets.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (ruleset is null)
        {
            // Already gone, or never visible from this organization. Either
            // way there is nothing here to remove.
            return Results.NotFound();
        }

        if (await usage.IsInUseAsync(id, cancellationToken))
        {
            return Results.Problem(
                detail: "A competition is being played under this ruleset, so it cannot be " +
                        "removed. Its results are read against these rules.",
                statusCode: StatusCodes.Status409Conflict);
        }

        database.Rulesets.Remove(ruleset);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A competition bound to it between the check above and this
            // write. The check produces the message; the foreign key produces
            // the guarantee, and it is the one that cannot be raced.
            return Results.Problem(
                detail: "A competition is being played under this ruleset, so it cannot be " +
                        "removed. Its results are read against these rules.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
