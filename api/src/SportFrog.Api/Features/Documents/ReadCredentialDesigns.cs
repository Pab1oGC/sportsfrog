using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// The credential designs of the organization, and one of them.
/// </summary>
/// <remarks>
/// The keys travel with the links. The editor shows the links and sends the
/// keys back untouched when nothing was re-uploaded, which is what
/// <see cref="CredentialDesignPictures"/> expects.
/// </remarks>
public static class ReadCredentialDesigns
{
    public sealed record Design(
        Guid Id,
        string Name,
        string? LegalText,
        string? AccentColorHex,
        bool IsDefault,
        string? BackgroundKey,
        string? LogoKey,
        string? BackgroundUrl,
        string? LogoUrl,
        DateTimeOffset UpdatedAt);

    public static IEndpointRouteBuilder MapReadCredentialDesigns(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/documents/credential-designs", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCredentialDesigns))
            .WithSummary("Lists the credential designs of the active organization.");

        routes.MapGet("/documents/credential-designs/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCredentialDesign")
            .WithSummary("Reads one credential design, with links to its pictures.");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var designs = await database.CredentialDesigns
            .AsNoTracking()
            .OrderByDescending(design => design.IsDefault)
            .ThenBy(design => design.Name)
            .ToListAsync(cancellationToken);

        var presented = new List<Design>(designs.Count);

        foreach (var design in designs)
        {
            presented.Add(await PresentAsync(design, pictures, cancellationToken));
        }

        return Results.Ok(presented);
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var design = await database.CredentialDesigns
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return design is null
            ? Results.NotFound()
            : Results.Ok(await PresentAsync(design, pictures, cancellationToken));
    }

    private static async Task<Design> PresentAsync(
        CredentialDesign design, PortalPicture pictures, CancellationToken cancellationToken) =>
        new(
            design.Id,
            design.Name,
            design.LegalText,
            design.AccentColorHex,
            design.IsDefault,
            design.BackgroundKey,
            design.LogoKey,
            await pictures.LinkAsync(design.BackgroundKey, cancellationToken),
            await pictures.LinkAsync(design.LogoKey, cancellationToken),
            design.UpdatedAt);
}
