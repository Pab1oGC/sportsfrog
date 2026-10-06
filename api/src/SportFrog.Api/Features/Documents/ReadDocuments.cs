using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// How a batch is going, what it produced, and how to get the paper.
/// </summary>
/// <remarks>
/// The other half of accepting work and answering straight away. An operator
/// who asks for four hundred credentials and is told "accepted" needs
/// somewhere to watch it, somewhere to find the sheet that goes to the
/// printer, and — for the people it passed over — a list naming them rather
/// than counting them. "Seven skipped" is not something anybody can act on.
/// "Pedro has no photograph" is.
/// </remarks>
public static class ReadDocuments
{
    public sealed record BatchSummary(
        Guid Id,
        DocumentKind Kind,
        DocumentBatchState Status,
        int Total,
        int Issued,
        int Skipped,
        bool HasSheet,
        string? Failure,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FinishedAt);

    public sealed record BatchDetail(
        Guid Id,
        DocumentKind Kind,
        Guid? TemplateId,
        int? TemplateVersion,
        DocumentBatchState Status,
        int Total,
        int Issued,
        int Skipped,
        string? SheetUrl,
        IReadOnlyList<DocumentProblem> Problems,
        string? Failure,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FinishedAt);

    /// <param name="PdfUrl">
    /// A temporary link to the document itself. It expires, and it is a
    /// bearer credential for that one file while it lasts.
    /// </param>
    public sealed record Issued(
        Guid Id,
        DocumentKind Kind,
        string SerialNumber,

        /// <summary>The credential's own printed identifier. Null on a certificate.</summary>
        string? VisibleId,

        string? Subject,
        string? TeamName,
        string? CertificateType,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        DocumentState Status,
        string? PdfUrl,
        DateTimeOffset IssuedAt);

    public static IEndpointRouteBuilder MapReadDocuments(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/documents/batches", ListBatchesAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadDocuments))
            .WithSummary("Lists the recent batches of documents.");

        routes.MapGet("/documents/batches/{id:guid}", ReadBatchAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadDocumentBatch")
            .WithSummary("Reads one batch, with the sheet and everybody it passed over.");

        routes.MapGet("/documents/issued", ListIssuedAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadIssuedDocuments")
            .WithSummary("Lists issued documents, with a link to each one's PDF.");

        return routes;
    }

    private static async Task<IResult> ListBatchesAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        int take = 20) =>
        Results.Ok(await database.DocumentBatches
            .AsNoTracking()
            .OrderByDescending(batch => batch.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(batch => new BatchSummary(
                batch.Id,
                batch.Kind,
                batch.Status,
                batch.Total,
                batch.Issued,
                batch.Skipped,
                batch.SheetKey != null,
                batch.Failure,
                batch.CreatedAt,
                batch.FinishedAt))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadBatchAsync(
        Guid id,
        SportFrogDbContext database,
        ObjectStore store,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var batch = await database.DocumentBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (batch is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new BatchDetail(
            batch.Id,
            batch.Kind,
            batch.TemplateId,
            batch.TemplateVersion,
            batch.Status,
            batch.Total,
            batch.Issued,
            batch.Skipped,
            batch.SheetKey is null
                ? null
                : await store.ReadLinkAsync(
                    organization.RequireOrganizationId(), batch.SheetKey, cancellationToken),
            batch.Problems,
            batch.Failure,
            batch.CreatedAt,
            batch.FinishedAt));
    }

    /// <summary>
    /// The documents themselves, narrowed by whatever the caller is looking at.
    /// </summary>
    /// <remarks>
    /// Every row carries a signed link, which costs nothing — signing is
    /// arithmetic and no call leaves the process — so a page listing a squad's
    /// credentials can offer every one of them without a second request.
    /// </remarks>
    private static async Task<IResult> ListIssuedAsync(
        SportFrogDbContext database,
        ObjectStore store,
        OrganizationContext organization,
        CancellationToken cancellationToken,
        Guid? batchId = null,
        Guid? competitionId = null,
        Guid? athleteId = null,
        int take = 100)
    {
        var documents = await database.IssuedDocuments
            .AsNoTracking()
            .Where(document => batchId == null || document.BatchId == batchId)
            .Where(document => competitionId == null || document.CompetitionId == competitionId)
            .Where(document => athleteId == null || document.AthleteId == athleteId)
            .OrderByDescending(document => document.IssuedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(document => new
            {
                document.Id,
                document.Kind,
                document.SerialNumber,
                document.VisibleId,
                Subject = document.Athlete!.LastName + " " + document.Athlete.FirstName,
                TeamName = document.Team!.Name,
                document.CertificateType,
                document.ValidFrom,
                document.ValidTo,
                document.Status,
                document.PdfUrl,
                document.IssuedAt,
            })
            .ToListAsync(cancellationToken);

        var organizationId = organization.RequireOrganizationId();
        var listing = new List<Issued>(documents.Count);

        foreach (var document in documents)
        {
            listing.Add(new Issued(
                document.Id,
                document.Kind,
                document.SerialNumber,
                document.VisibleId,
                document.Subject,
                document.TeamName,
                document.CertificateType,
                document.ValidFrom,
                document.ValidTo,
                document.Status,
                document.PdfUrl is null
                    ? null
                    : await store.ReadLinkAsync(organizationId, document.PdfUrl, cancellationToken),
                document.IssuedAt));
        }

        return Results.Ok(listing);
    }
}
