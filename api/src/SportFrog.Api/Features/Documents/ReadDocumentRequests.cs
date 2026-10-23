using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// What an organization has asked SportFrog to print, and where each ask
/// stands.
/// </summary>
public static class ReadDocumentRequests
{
    public sealed record Summary(
        Guid Id,
        DocumentKind Kind,
        Guid? TemplateId,
        string? CertificateType,
        Guid CompetitionId,
        Guid? CategoryId,
        Guid? TeamId,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        DocumentRequestState Status,
        DateTimeOffset CreatedAt);

    public static IEndpointRouteBuilder MapReadDocumentRequests(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/document-requests", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadDocumentRequests))
            .WithSummary("Lists this organization's requests for documents.");

        routes.MapGet("/document-requests/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadDocumentRequest")
            .WithSummary("Reads one request for documents.");

        return routes;
    }

    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        int take = 20) =>
        Results.Ok(await database.DocumentRequests
            .AsNoTracking()
            .OrderByDescending(request => request.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(request => new Summary(
                request.Id,
                request.Kind,
                request.TemplateId,
                request.CertificateType,
                request.CompetitionId,
                request.CategoryId,
                request.TeamId,
                request.ValidFrom,
                request.ValidTo,
                request.Status,
                request.CreatedAt))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var request = await database.DocumentRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return request is null
            ? Results.NotFound()
            : Results.Ok(new Summary(
                request.Id,
                request.Kind,
                request.TemplateId,
                request.CertificateType,
                request.CompetitionId,
                request.CategoryId,
                request.TeamId,
                request.ValidFrom,
                request.ValidTo,
                request.Status,
                request.CreatedAt));
    }
}
