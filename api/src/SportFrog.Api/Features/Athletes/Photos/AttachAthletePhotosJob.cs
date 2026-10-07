using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Athletes.Photos;

/// <summary>
/// Attaches an uploaded archive of photographs to the people it names.
/// </summary>
/// <remarks>
/// Runs after the request that accepted the archive has already answered.
/// Four hundred photographs are four hundred decodes, four hundred resizes
/// and four hundred uploads; the operator is not going to watch a spinner
/// through that, and a request that tried would be killed by a proxy long
/// before it finished.
///
/// It runs in three parts on purpose. Reading what to do is one transaction,
/// the slow middle holds no transaction at all, and recording what was done
/// is a third. Wrapping the whole thing would hold a database transaction
/// open for minutes while the process talks to object storage, which is a
/// long-running lock bought for nothing.
/// </remarks>
/// <summary>
/// A batch that cannot succeed however many times it is tried.
/// </summary>
/// <remarks>
/// The archive is corrupt, or too big, or holds no photographs at all.
/// Retrying is not going to change any of that, so it is recorded against the
/// batch and the job is allowed to end — as opposed to an unreachable bucket
/// or a lost connection, which is worth trying again and which the queue
/// retries by throwing.
/// </remarks>
internal sealed class PhotoBatchRefused(string reason) : Exception(reason);

public sealed class AttachAthletePhotosJob(
    OrganizationJobScope scopes,
    ObjectStore store,
    ILogger<AttachAthletePhotosJob> logger)
{
    /// <summary>What the batch is, and who it is about.</summary>
    private sealed record Work(
        string ArchiveKey,
        IReadOnlyList<Candidate> Candidates);

    private sealed record Candidate(Guid Id, string DocumentId, string Name, string? PhotoKey);

    /// <summary>A photograph stored and waiting to be pointed at.</summary>
    private sealed record Attachment(Guid AthleteId, StoredAthletePhoto Photo, string? Replaced);

    /// <param name="organizationId">
    /// Which organization this belongs to. Carried as an argument because a
    /// job has no token to derive it from; row-level security is what then
    /// checks the claim, so a mismatched pair finds no batch at all.
    /// </param>
    public async Task RunAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var work = await StartAsync(organizationId, userId, batchId, cancellationToken);

        if (work is null)
        {
            // The row is not there. After the response has been sent this can
            // only mean the batch was never committed, so there is nothing to
            // retry and nothing to fail: the work simply does not exist.
            logger.LogWarning(
                "Photo batch {Batch} of organization {Organization} was not found.",
                batchId, organizationId);

            return;
        }

        try
        {
            var (results, attachments) =
                await ProcessAsync(organizationId, userId, work, cancellationToken);

            await FinishAsync(
                organizationId, userId, batchId, results, attachments, cancellationToken);
        }
        catch (PhotoBatchRefused refused)
        {
            // Nothing wrong with the system; something wrong with what was
            // uploaded. Recorded against the batch and left there. Throwing
            // would put a corrupt archive through ten retries to reach the
            // same answer, and leave it sitting in the queue's failed list as
            // though somebody ought to look into it.
            logger.LogInformation(
                "Photo batch {Batch} was refused: {Reason}", batchId, refused.Message);

            await FailAsync(organizationId, userId, batchId, refused.Message, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "Photo batch {Batch} could not be processed.", batchId);

            await FailAsync(organizationId, userId, batchId, failure.Message, cancellationToken);

            // Rethrown so the queue retries it. This is the other kind: an
            // unreachable bucket, a dropped connection — things that are
            // often better a minute later.
            throw;
        }
    }

    /// <summary>
    /// Marks the batch as running and reads everything the slow part needs.
    /// </summary>
    /// <remarks>
    /// The whole register of the organization is read here, not one athlete
    /// per file. A thousand lookups against a table is a thousand round trips
    /// to answer a question one round trip answers, and the register of a
    /// league is small enough to hold.
    /// </remarks>
    private async Task<Work?> StartAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await scopes.RunAsync(organizationId, userId, async (_, database) =>
        {
            var batch = await database.PhotoImports
                .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

            if (batch is null)
            {
                return null;
            }

            batch.Status = PhotoImportState.Running;

            var candidates = await database.Athletes
                .AsNoTracking()
                .Select(athlete => new Candidate(
                    athlete.Id,
                    athlete.DocumentId,
                    athlete.LastName + " " + athlete.FirstName,
                    athlete.PhotoKey))
                .ToListAsync(cancellationToken);

            await database.SaveChangesAsync(cancellationToken);

            return new Work(batch.ArchiveKey, candidates);
        },
        cancellationToken);

    /// <summary>
    /// The slow middle: open the archive, and store every photograph that
    /// names somebody.
    /// </summary>
    private async Task<(List<PhotoResult> Results, List<Attachment> Attachments)> ProcessAsync(
        Guid organizationId,
        Guid userId,
        Work work,
        CancellationToken cancellationToken)
    {
        var archive = await store.ReadAsync(organizationId, work.ArchiveKey, cancellationToken);

        using var contents = new MemoryStream(archive, writable: false);
        var opened = PhotoArchive.Read(contents);

        if (opened.Problem is { } problem)
        {
            throw new PhotoBatchRefused(problem);
        }

        // An archive with no images in it at all is somebody who uploaded the
        // wrong file, and saying so is the only useful answer. It is not a
        // hypothetical: a .xlsx is itself a zip, so the squad spreadsheet
        // opens perfectly here and yields a dozen entries that are not
        // photographs. Reporting that as a finished batch with twelve
        // failures would be technically true and completely unhelpful.
        if (opened.Photos.Count == 0)
        {
            throw new PhotoBatchRefused(
                "El comprimido no tiene ninguna imagen. Tiene que ser un .zip con las fotos "
                    + "adentro, nombradas con el documento de cada persona.");
        }

        var register = Register(work.Candidates);

        var results = new List<PhotoResult>(opened.Photos.Count + opened.Ignored.Count);
        var attachments = new List<Attachment>();
        var taken = new HashSet<Guid>();

        results.AddRange(opened.Ignored.Select(
            name => new PhotoResult(name, PhotoOutcome.Ignored, null)));

        return await scopes.RunDetachedAsync(organizationId, userId, async services =>
        {
            var photos = services.GetRequiredService<AthletePhoto>();

            foreach (var photo in opened.Photos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!register.TryGetValue(photo.Key, out var athlete))
                {
                    results.Add(new PhotoResult(photo.Name, PhotoOutcome.Unmatched, null));
                    continue;
                }

                // Two files naming the same person. The first one wins rather
                // than the last, because an archive is usually built by
                // dragging a folder in and the duplicate is the copy — and
                // whichever rule is chosen, saying which file was skipped is
                // what lets the operator settle it.
                if (!taken.Add(athlete.Id))
                {
                    results.Add(new PhotoResult(photo.Name, PhotoOutcome.Duplicate, athlete.Name));
                    continue;
                }

                if (await photos.StoreAsync(athlete.Id, photo.Content, cancellationToken)
                    is not { } stored)
                {
                    taken.Remove(athlete.Id);
                    results.Add(new PhotoResult(photo.Name, PhotoOutcome.Unreadable, athlete.Name));
                    continue;
                }

                // Rejected photographs are never kept, here or anywhere else.
                // Released so a later photograph for the same person still counts.
                if (stored.Key is null)
                {
                    taken.Remove(athlete.Id);
                    results.Add(new PhotoResult(photo.Name, PhotoOutcome.Rejected, athlete.Name));
                    continue;
                }

                attachments.Add(new Attachment(athlete.Id, stored, athlete.PhotoKey));

                results.Add(new PhotoResult(photo.Name, PhotoOutcome.Attached, athlete.Name));
            }

            return (results, attachments);
        });
    }

    /// <summary>
    /// Points the rows at what was stored, and writes down how it went.
    /// </summary>
    /// <remarks>
    /// One transaction for the whole batch. The photographs are already in
    /// object storage by now, so if this fails nothing is lost that a re-run
    /// would not simply store again — whereas rows updated one at a time
    /// would leave a batch half attached with no way to tell which half.
    /// </remarks>
    private async Task FinishAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        List<PhotoResult> results,
        List<Attachment> attachments,
        CancellationToken cancellationToken)
    {
        var replaced = await scopes.RunAsync(organizationId, userId, async (_, database) =>
        {
            var pointed = new List<string>();

            foreach (var attachment in attachments)
            {
                var athlete = await database.Athletes.SingleOrDefaultAsync(
                    candidate => candidate.Id == attachment.AthleteId, cancellationToken);

                if (athlete is null)
                {
                    // Deleted between the read and now. Rare, and not worth
                    // failing a batch of four hundred over.
                    continue;
                }

                if (attachment.Replaced is { Length: > 0 } previous
                    && !previous.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    pointed.Add(previous);
                }

                athlete.PhotoKey = attachment.Photo.Key;
                attachment.Photo.Assessment.ApplyTo(athlete);
            }

            var batch = await database.PhotoImports.SingleAsync(
                candidate => candidate.Id == batchId, cancellationToken);

            batch.Results = results;

            // The counts are about photographs, not about entries. A file
            // that was never an image is listed so nobody wonders where it
            // went, but calling it a failure would put "eleven failed" on a
            // batch where eleven of the entries were a folder's worth of
            // stray text files.
            batch.Matched = results.Count(result => result.Outcome == PhotoOutcome.Attached);
            batch.Failed = results.Count(result =>
                result.Outcome is PhotoOutcome.Unmatched
                    or PhotoOutcome.Unreadable
                    or PhotoOutcome.Duplicate
                    or PhotoOutcome.Rejected);
            batch.Total = batch.Matched + batch.Failed;
            batch.Status = PhotoImportState.Finished;
            batch.FinishedAt = DateTimeOffset.UtcNow;

            await database.SaveChangesAsync(cancellationToken);

            return pointed;
        },
        cancellationToken);

        // Only after the rows point somewhere else, and never as a reason to
        // fail: an old photograph nothing references is wasted space, not a
        // problem worth reporting to anybody.
        foreach (var key in replaced)
        {
            await store.ForgetAsync(organizationId, key, cancellationToken);
        }
    }

    private async Task FailAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await scopes.RunAsync(organizationId, userId, async (_, database) =>
            {
                var batch = await database.PhotoImports.SingleOrDefaultAsync(
                    candidate => candidate.Id == batchId, cancellationToken);

                if (batch is not null)
                {
                    batch.Status = PhotoImportState.Failed;
                    batch.Failure = reason;
                    batch.FinishedAt = DateTimeOffset.UtcNow;

                    await database.SaveChangesAsync(cancellationToken);
                }

                return true;
            },
            cancellationToken);
        }
        catch (Exception writing)
        {
            // The batch stays as running and the original failure is still
            // thrown. Losing the reason is bad; hiding the failure behind an
            // error about recording it would be worse.
            logger.LogError(writing, "Could not record the failure of photo batch {Batch}.", batchId);
        }
    }

    /// <summary>
    /// The register, keyed the way a file name is.
    /// </summary>
    /// <remarks>
    /// Two documents that reduce to the same key — <c>V-101</c> and
    /// <c>V101</c> on two different people — are both dropped rather than one
    /// being picked. A photograph attached to the wrong child is worse than a
    /// photograph not attached at all, and the file will be reported as
    /// matching nobody, which is at least true.
    /// </remarks>
    private static Dictionary<string, Candidate> Register(IReadOnlyList<Candidate> candidates)
    {
        var register = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var key = PhotoArchive.MatchKey(candidate.DocumentId);

            if (key.Length == 0)
            {
                continue;
            }

            if (!register.TryAdd(key, candidate))
            {
                ambiguous.Add(key);
            }
        }

        foreach (var key in ambiguous)
        {
            register.Remove(key);
        }

        return register;
    }
}
