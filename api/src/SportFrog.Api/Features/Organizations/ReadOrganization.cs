using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Organizations;

/// <summary>Reads the active organization's own profile.</summary>
public static class ReadOrganization
{
    /// <param name="LogoUrl">A temporary link to the mark, not the mark itself.</param>
    public sealed record Response(Guid Id, string Name, string Slug, string? LogoUrl);

    public static IEndpointRouteBuilder MapReadOrganization(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/organizations", HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadOrganization))
            .WithSummary("Reads the active organization.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        SportFrogDbContext database,
        OrganizationContext organization,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var org = await database.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Id == organization.RequireOrganizationId())
            .SingleAsync(cancellationToken);

        return Results.Ok(new Response(
            org.Id, org.Name, org.Slug, await pictures.LinkAsync(org.LogoUrl, cancellationToken)));
    }
}
