using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

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
/// </remarks>
public static class DeleteTemplate
{
    public static IEndpointRouteBuilder MapDeleteTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/documents/templates/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteTemplate))
            .WithSummary("Retires a design, keeping everything issued from it.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var template = await database.Set<DocumentTemplate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (template is null)
        {
            return Results.NotFound();
        }

        template.DeletedAt = clock.GetUtcNow();

        // A retired design cannot go on being the one offered first. Left set,
        // it would be a default that no listing shows and nothing can pick.
        template.IsDefault = false;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
