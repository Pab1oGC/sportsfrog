using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Refuses a pending request for documents.
/// </summary>
/// <remarks>
/// Runs inside <see cref="OrganizationJobScope"/> for the same reason
/// <see cref="ApproveDocumentRequest"/> does: this acts on the asking
/// organization's own row, never the platform's.
/// </remarks>
public static class RejectDocumentRequest
{
    public sealed record Request(string Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("Hay que decir por qué se rechaza.")
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapRejectDocumentRequest(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/document-requests/{id:guid}/reject", HandleAsync)
            .WithoutOrganizationContext()
            .WithName(nameof(RejectDocumentRequest))
            .WithSummary("Refuses a pending request for documents.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationJobScope scopes,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                detail: "Se requiere autenticación.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!await PlatformAuthority.ReachesAsync(
                context.User, database, MembershipRole.Operator, cancellationToken))
        {
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var organizationId = await PlatformDocumentRequestReader.ResolveOrganizationAsync(
            database, id, cancellationToken);

        if (organizationId is not { } orgId)
        {
            return Results.NotFound();
        }

        var reviewerId = JwtAccessTokenIssuer.CurrentUserId(context.User);

        return await scopes.RunAsync(orgId, reviewerId, async (_, tenantDb) =>
            {
                var documentRequest = await tenantDb.DocumentRequests
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

                if (documentRequest is null)
                {
                    return Results.NotFound();
                }

                if (documentRequest.Status != DocumentRequestState.Pending)
                {
                    return Results.Problem(
                        detail: "Esa solicitud ya no está pendiente.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                documentRequest.Status = DocumentRequestState.Rejected;
                documentRequest.ReviewedBy = reviewerId;
                documentRequest.ReviewedAt = clock.GetUtcNow();
                documentRequest.RejectionReason = request.Reason.Trim();

                await tenantDb.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            },
            cancellationToken);
    }
}
