using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Removes a category that nothing has been entered into.
/// </summary>
/// <remarks>
/// Physical, because the table carries no deleted_at: a category is part of
/// the shape of a competition rather than a record of an event, and one that
/// never fielded a team leaves nothing behind.
/// </remarks>
public static class DeleteCategory
{
    public static IEndpointRouteBuilder MapDeleteCategory(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/competitions/{competitionId:guid}/categories/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(DeleteCategory))
            .WithSummary("Removes a category that has no teams or matches.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        CategoryUsage usage,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.Id == id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        if (await usage.IsInUseAsync(id, cancellationToken))
        {
            return Results.Problem(
                detail: "Teams or matches have been entered into this category, so it cannot be " +
                        "removed. Withdraw the competition instead if the whole event is being " +
                        "discarded.",
                statusCode: StatusCodes.Status409Conflict);
        }

        database.Categories.Remove(category);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // A team entered between the check above and this write. The
            // check covers matches, which the database would have cascaded
            // away without complaint; this covers teams, which it refuses.
            return Results.Problem(
                detail: "Teams or matches have been entered into this category, so it cannot be " +
                        "removed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}
