using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>Removes a credential design.</summary>
/// <remarks>
/// The competitions that chose it fall back to the organization's default, by
/// the database's own rule on the reference. Credentials already printed are
/// untouched: each batch froze its values when it was requested.
/// </remarks>
public static class DeleteCredentialDesign
{
    public static IEndpointRouteBuilder MapDeleteCredentialDesign(this IEndpointRouteBuilder routes)
    {
        routes.MapDelete("/documents/credential-designs/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(DeleteCredentialDesign))
            .WithSummary("Removes a credential design. Its competitions fall back to the default.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var design = await database.CredentialDesigns
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (design is null)
        {
            return Results.NotFound();
        }

        database.CredentialDesigns.Remove(design);
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
