using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Athletes.Photos;

/// <summary>
/// How a batch of photographs is going, and how the last few went.
/// </summary>
/// <remarks>
/// The other half of accepting work and answering straight away: an operator
/// who uploads four hundred photographs and is told "accepted" needs
/// somewhere to find out what happened to them, and it has to name the files
/// rather than count them. "Seven failed" is not something anybody can act
/// on. "IMG_2831.jpg matched nobody" is a file to rename.
/// </remarks>
public static class ReadPhotoImports
{
    public sealed record Summary(
        Guid Id,
        string FileName,
        PhotoImportState Status,
        int Total,
        int Matched,
        int Failed,
        string? Failure,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FinishedAt);

    public sealed record Detail(
        Guid Id,
        string FileName,
        PhotoImportState Status,
        int Total,
        int Matched,
        int Failed,
        string? Failure,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FinishedAt,
        IReadOnlyList<PhotoResult> Results);

    public static IEndpointRouteBuilder MapReadPhotoImports(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/athletes/photos/imports", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadPhotoImports))
            .WithSummary("Lists the recent batches of photographs.");

        routes.MapGet("/athletes/photos/imports/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadPhotoImport")
            .WithSummary("Reads one batch, file by file.");

        return routes;
    }

    /// <summary>
    /// The recent batches, without the file-by-file detail.
    /// </summary>
    /// <remarks>
    /// The results of a thousand-file archive are not something a listing
    /// should carry: a page showing five batches would move five thousand
    /// entries to display ten numbers.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        int take = 20) =>
        Results.Ok(await database.PhotoImports
            .AsNoTracking()
            .OrderByDescending(batch => batch.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(batch => new Summary(
                batch.Id,
                batch.FileName,
                batch.Status,
                batch.Total,
                batch.Matched,
                batch.Failed,
                batch.Failure,
                batch.CreatedAt,
                batch.FinishedAt))
            .ToListAsync(cancellationToken));

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var batch = await database.PhotoImports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return batch is null
            ? Results.NotFound()
            : Results.Ok(new Detail(
                batch.Id,
                batch.FileName,
                batch.Status,
                batch.Total,
                batch.Matched,
                batch.Failed,
                batch.Failure,
                batch.CreatedAt,
                batch.FinishedAt,
                batch.Results));
    }
}
