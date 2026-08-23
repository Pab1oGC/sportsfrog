using Hangfire;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Athletes.Photos;

/// <summary>
/// Accepts an archive of photographs and gets on with it later.
/// </summary>
/// <remarks>
/// Separate from the squad spreadsheet on purpose. The sheet comes from a
/// club secretary and the photographs come later, from somebody else, and
/// incomplete; making them one upload would mean waiting for both. So this
/// takes an archive on its own, at any time, and attaches whatever it can.
///
/// Files are matched to people by identity document — the name of the file is
/// the number. Nothing else would survive the round trip: an invented
/// identifier has to be typed into the spreadsheet and onto the file and
/// agree in both places, and the document is already in both.
/// </remarks>
public static class ImportAthletePhotos
{
    /// <summary>
    /// Big enough for a few hundred photographs from a phone. What it
    /// decompresses to is bounded separately, and by what is actually read
    /// rather than by what the archive claims.
    /// </summary>
    private const long MaximumUpload = 150L * 1024 * 1024;

    public sealed record Response(Guid Id, PhotoImportState Status);

    public static IEndpointRouteBuilder MapImportAthletePhotos(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/athletes/photos/imports", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(ImportAthletePhotos))
            .WithSummary("Accepts a zip of photographs, matched to people by identity document.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        IFormFile file,
        HttpContext context,
        SportFrogDbContext database,
        ObjectStore store,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Refuse("El archivo está vacío.");
        }

        if (file.Length > MaximumUpload)
        {
            return Refuse($"El comprimido pesa más de {MaximumUpload / (1024 * 1024)} MB.");
        }

        var organizationId = organization.RequireOrganizationId();
        var batchId = Guid.NewGuid();
        var key = StorageKeys.PhotoArchive(organizationId, batchId);

        using (var contents = file.OpenReadStream())
        {
            using var buffer = new MemoryStream();
            await contents.CopyToAsync(buffer, cancellationToken);

            // Put away before the row is written, so a row never points at an
            // archive that was never stored. The reverse leaves a batch the
            // worker cannot open.
            await store.PutAsync(key, buffer.ToArray(), "application/zip", cancellationToken);
        }

        var batch = new PhotoImport
        {
            Id = batchId,
            OrgId = organizationId,
            FileName = Path.GetFileName(file.FileName),
            ArchiveKey = key,
            RequestedBy = organization.UserId ?? Guid.Empty,
        };

        database.PhotoImports.Add(batch);
        await database.SaveChangesAsync(cancellationToken);

        Queue(context, jobs, organizationId, batch.RequestedBy, batchId);

        return Results.Accepted(
            $"/athletes/photos/imports/{batchId}",
            new Response(batchId, PhotoImportState.Queued));
    }

    /// <summary>
    /// Enqueues the work once the response has actually gone out.
    /// </summary>
    /// <remarks>
    /// Not at the end of the handler, which would be a race with a sharp
    /// edge. Every request runs inside one transaction that the entry channel
    /// commits after the handler returns; the queue writes on its own
    /// connection and a worker is free to pick the job up immediately. Queued
    /// inline, the job can therefore start before the row it is about is
    /// visible to anybody, and find nothing.
    ///
    /// Waiting for the response to complete puts the enqueue strictly after
    /// that commit. It also means a request that is rolled back later never
    /// queues anything, which is the behaviour anybody would expect and not
    /// what the obvious version does.
    /// </remarks>
    private static void Queue(
        HttpContext context,
        IBackgroundJobClient jobs,
        Guid organizationId,
        Guid userId,
        Guid batchId) =>
        context.Response.OnCompleted(() =>
        {
            jobs.Enqueue<AttachAthletePhotosJob>(job =>
                job.RunAsync(organizationId, userId, batchId, CancellationToken.None));

            return Task.CompletedTask;
        });

    private static IResult Refuse(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["file"] = [message],
        });
}
