using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Every organization's pending ask for documents, in one list.
/// </summary>
/// <remarks>
/// Read by the platform organization, never by the organization that asked:
/// an ordinary request only ever sees its own rows, through
/// tenant_isolation. This crosses every one of them at once, through
/// <see cref="PlatformDocumentRequestReader"/> — see that type's own
/// remarks for how.
/// </remarks>
public static class ReadPendingDocumentRequests
{
    public static IEndpointRouteBuilder MapReadPendingDocumentRequests(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/document-requests/pending", HandleAsync)
            // Like RegisterOrganization: this does not act inside the
            // caller's own organization, it acts across every one of them,
            // so there is no organization for RequireRole to check a role
            // in. Checked by hand instead, against the platform
            // organization.
            .WithoutOrganizationContext()
            .WithName(nameof(ReadPendingDocumentRequests))
            .WithSummary("Lists every organization's pending request for documents.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                detail: "Se requiere autenticación.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!await PlatformAuthority.ReachesAsync(
                context.User, database, MembershipRole.Viewer, cancellationToken))
        {
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(await PlatformDocumentRequestReader.ReadPendingAsync(database, cancellationToken));
    }
}
