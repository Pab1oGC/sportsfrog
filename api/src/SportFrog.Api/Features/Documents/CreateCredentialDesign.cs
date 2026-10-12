using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>Creates a credential design for the organization.</summary>
public static class CreateCredentialDesign
{
    public sealed record Response(Guid Id, bool IsDefault);

    public static IEndpointRouteBuilder MapCreateCredentialDesign(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/documents/credential-designs", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(CreateCredentialDesign))
            .WithSummary("Creates a credential design: the legal notice, background, accent and fallback logo.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        CredentialDesignContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var (resolved, refused) = await CredentialDesignPictures.ResolveAsync(
            contract, pictures, organization, cancellationToken);

        if (resolved is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [refused!] = ["No se pudo leer la imagen, o no pertenece a esta organización."],
            });
        }

        var design = new CredentialDesign
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            Name = contract.Name.Trim(),
            LegalText = contract.LegalText?.Trim(),
            BackgroundKey = resolved.BackgroundKey,
            AccentColorHex = contract.AccentColorHex,
            LogoKey = resolved.LogoKey,
            IsDefault = await CredentialDesignDefaults.ShouldBeDefaultAsync(
                database, contract.IsDefault, cancellationToken),
        };

        await CredentialDesignDefaults.ClearOtherDefaultsAsync(database, design, cancellationToken);

        database.CredentialDesigns.Add(design);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/documents/credential-designs/{design.Id}",
            new Response(design.Id, design.IsDefault));
    }
}
