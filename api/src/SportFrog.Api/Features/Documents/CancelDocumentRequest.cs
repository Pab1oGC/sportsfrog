using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Withdraws a request for documents before SportFrog has looked at it.
/// </summary>
/// <remarks>
/// Like <see cref="RevokeDocument"/>, this does not delete the row: a request
/// somebody acted on while it was still pending would otherwise disappear
/// from under that decision. It stays, and it answers "cancelled".
/// </remarks>
public static class CancelDocumentRequest
{
    public static IEndpointRouteBuilder MapCancelDocumentRequest(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/document-requests/{id:guid}/cancel", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CancelDocumentRequest))
            .WithSummary("Withdraws a request for documents that is still pending.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var request = await database.DocumentRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (request is null)
        {
            return Results.NotFound();
        }

        if (request.Status != DocumentRequestState.Pending)
        {
            // Only a pending ask can be withdrawn: one already approved has a
            // batch behind it, and one already rejected or cancelled has
            // nothing left to withdraw.
            return Results.Problem(
                detail: "Esa solicitud ya no está pendiente.",
                statusCode: StatusCodes.Status409Conflict);
        }

        request.Status = DocumentRequestState.Cancelled;
        request.CancelledAt = clock.GetUtcNow();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
