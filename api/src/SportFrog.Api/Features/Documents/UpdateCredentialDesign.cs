using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>Corrects a credential design.</summary>
/// <remarks>
/// Replaced pictures are deliberately not deleted from storage. A batch
/// requested before the change froze the old key, and reprinting that batch
/// must still produce the card it produced then. The cost is an unused image,
/// the same trade the certificate artwork already makes.
/// </remarks>
public static class UpdateCredentialDesign
{
    public static IEndpointRouteBuilder MapUpdateCredentialDesign(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/documents/credential-designs/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(UpdateCredentialDesign))
            .WithSummary("Corrects a credential design.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        CredentialDesignContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var design = await database.CredentialDesigns
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (design is null)
        {
            return Results.NotFound();
        }

        var (resolved, refused) = await CredentialDesignPictures.ResolveAsync(
            contract, pictures, organization, cancellationToken);

        if (resolved is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [refused!] = ["No se pudo leer la imagen, o no pertenece a esta organización."],
            });
        }

        design.Name = contract.Name.Trim();
        design.LegalText = contract.LegalText?.Trim();
        design.BackgroundKey = resolved.BackgroundKey;
        design.AccentColorHex = contract.AccentColorHex;
        design.LogoKey = resolved.LogoKey;

        // Only ever made default, never unmade by an edit: an organization
        // with a default keeps one, and changing which is a deliberate act of
        // choosing another design as the default.
        if (contract.IsDefault)
        {
            design.IsDefault = true;
            await CredentialDesignDefaults.ClearOtherDefaultsAsync(database, design, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
