using Hangfire;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Accepts a pending request, raising the batch that will print it.
/// </summary>
/// <remarks>
/// The platform's own counterpart to <see cref="RequestDocumentBatch"/>: what
/// gets pinned onto the batch is built exactly the same way that one builds
/// it for itself — <see cref="RequestDocumentBatch.BuildSnapshotAsync"/> for
/// a credential, <see cref="RequestDocumentBatch.ResolveTemplateAsync"/> for
/// a certificate — only who may trigger it, and when, differ. A design is
/// resolved again here rather than trusted from the request: the request
/// named a real design when it was raised, but approval can come later, and
/// a design can be retired in between.
///
/// Runs inside <see cref="OrganizationJobScope"/> because approving acts on
/// the asking organization's own data, never the platform's: the same
/// reason a job has to carry its organization as an argument rather than
/// read one from the request.
/// </remarks>
public static class ApproveDocumentRequest
{
    public sealed record Response(Guid RequestId, Guid BatchId, DocumentBatchState BatchStatus);

    public static IEndpointRouteBuilder MapApproveDocumentRequest(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/document-requests/{id:guid}/approve", HandleAsync)
            .WithoutOrganizationContext()
            .WithName(nameof(ApproveDocumentRequest))
            .WithSummary("Approves a pending request, raising a batch to print it.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationJobScope scopes,
        IBackgroundJobClient jobs,
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

        var outcome = await scopes.RunAsync(orgId, reviewerId, async (_, tenantDb) =>
            {
                var request = await tenantDb.DocumentRequests
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

                if (request is null)
                {
                    return (Response: Results.NotFound(), BatchId: (Guid?)null, RequestedBy: (Guid?)null);
                }

                if (request.Status != DocumentRequestState.Pending)
                {
                    return (
                        Response: Results.Problem(
                            detail: "Esa solicitud ya no está pendiente.",
                            statusCode: StatusCodes.Status409Conflict),
                        BatchId: (Guid?)null,
                        RequestedBy: (Guid?)null);
                }

                DocumentTemplate? template = null;
                CredentialSnapshot? snapshot = null;

                if (request.Kind == DocumentKind.Certificate)
                {
                    var resolved = await RequestDocumentBatch.ResolveTemplateAsync(
                        tenantDb, request.TemplateId!.Value, DocumentKind.Certificate, cancellationToken);

                    if (resolved.Refusal is not null)
                    {
                        return (Response: resolved.Refusal, BatchId: (Guid?)null, RequestedBy: (Guid?)null);
                    }

                    template = resolved.Template;
                }
                else
                {
                    // Guaranteed to exist: document_requests.competition_id
                    // references competitions with ON DELETE RESTRICT.
                    var competition = await tenantDb.Competitions
                        .AsNoTracking()
                        .SingleAsync(candidate => candidate.Id == request.CompetitionId, cancellationToken);

                    var resolved = await RequestDocumentBatch.BuildSnapshotAsync(
                        tenantDb, competition, cancellationToken);

                    if (resolved.Refusal is not null)
                    {
                        return (Response: resolved.Refusal, BatchId: (Guid?)null, RequestedBy: (Guid?)null);
                    }

                    snapshot = resolved.Snapshot;
                }

                var batch = new DocumentBatch
                {
                    Id = Guid.NewGuid(),
                    OrgId = orgId,
                    Kind = request.Kind,

                    // Pinned now, one way or the other — the same moment
                    // RequestDocumentBatch pins it for a batch it raises
                    // directly.
                    TemplateId = template?.Id,
                    TemplateVersion = template?.Version,
                    CredentialSnapshot = snapshot,

                    CompetitionId = request.CompetitionId,
                    CategoryId = request.CategoryId,
                    TeamId = request.TeamId,
                    CertificateType = request.CertificateType,
                    ValidFrom = request.ValidFrom,
                    ValidTo = request.ValidTo,
                    RequestedBy = request.RequestedBy,
                };

                tenantDb.DocumentBatches.Add(batch);

                request.Status = DocumentRequestState.Approved;
                request.ReviewedBy = reviewerId;
                request.ReviewedAt = clock.GetUtcNow();
                request.BatchId = batch.Id;

                await tenantDb.SaveChangesAsync(cancellationToken);

                return (
                    Response: Results.Ok(new Response(request.Id, batch.Id, batch.Status)),
                    BatchId: (Guid?)batch.Id,
                    RequestedBy: (Guid?)batch.RequestedBy);
            },
            cancellationToken);

        if (outcome.BatchId is { } batchId && outcome.RequestedBy is { } requestedBy)
        {
            // Only once the transaction above has actually committed, the
            // same reason RequestDocumentBatch waits for its response to go
            // out: the job cannot start before the row it is about is
            // visible.
            jobs.Enqueue<IssueDocumentsJob>(job =>
                job.RunAsync(orgId, requestedBy, batchId, CancellationToken.None));
        }

        return outcome.Response;
    }
}
