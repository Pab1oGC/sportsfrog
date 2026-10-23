using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Retires a design.
/// </summary>
/// <remarks>
/// Logically, always, and this is one of the few places in the system where
/// the schema itself insists on it: issued documents reference the template
/// and the version they were printed from, so the row is what makes a
/// credential reprintable years later. Erasing it would erase the record that
/// somebody was ever accredited.
///
/// So this is "stop offering it", not "undo it". The design disappears from
/// the listings and from anything about to print, and every card already
/// issued under it still resolves.
///
/// Platform-only, like <see cref="CreateTemplate"/> and
/// <see cref="UpdateTemplate"/>, and resolved the same cross-tenant way —
/// see <see cref="UpdateTemplate"/>'s own remarks.
/// </remarks>
public static class DeleteTemplate
{
    public static IEndpointRouteBuilder MapDeleteTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/documents/templates/{id:guid}", HandleAsync)
            .WithoutOrganizationContext()
            .WithName(nameof(DeleteTemplate))
            .WithSummary("Retires a design, keeping everything issued from it.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationJobScope scopes,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await PlatformAuthority.ReachesAsync(
                context.User, database, MembershipRole.Operator, cancellationToken))
        {
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (await PlatformTemplateReader.ResolveOrganizationAsync(database, id, cancellationToken)
            is not { } organizationId)
        {
            return Results.NotFound();
        }

        var callerId = JwtAccessTokenIssuer.CurrentUserId(context.User);

        return await scopes.RunAsync(organizationId, callerId, async (_, tenantDb) =>
        {
            var template = await tenantDb.Set<DocumentTemplate>()
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (template is null)
            {
                return Results.NotFound();
            }

            template.DeletedAt = clock.GetUtcNow();

            // A retired design cannot go on being the one offered first. Left
            // set, it would be a default that no listing shows and nothing
            // can pick.
            template.IsDefault = false;

            await tenantDb.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }, cancellationToken);
    }
}
