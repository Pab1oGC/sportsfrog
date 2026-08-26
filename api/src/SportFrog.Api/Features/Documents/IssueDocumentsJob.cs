using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// A batch that cannot succeed however many times it is tried.
/// </summary>
/// <remarks>
/// The design was retired, the competition has nobody in it, the artwork is
/// gone. Recorded against the batch and left there, as opposed to an
/// unreachable bucket, which is worth trying again and which the queue retries
/// by throwing.
/// </remarks>
internal sealed class DocumentBatchRefused(string reason) : Exception(reason);

/// <summary>
/// Prints a batch of documents.
/// </summary>
/// <remarks>
/// The heaviest work in the system, and the reason the queue exists. Each card
/// decodes a photograph, draws a layout over artwork, builds a QR code and
/// composes a PDF; four hundred of them is minutes, and no browser waits.
///
/// Three parts, like every job here. Reading what to print is one transaction,
/// the drawing holds no transaction at all, and recording what was printed is
/// a third. The middle part is where the minutes go, and holding a database
/// transaction across it would be a long-running lock bought for nothing.
/// </remarks>
public sealed class IssueDocumentsJob(
    OrganizationJobScope scopes,
    ObjectStore store,
    IOptions<DocumentOptions> options,
    ILogger<IssueDocumentsJob> logger)
{
    /// <summary>Everything the drawing needs, read in one go.</summary>
    private sealed record Work(
        TemplateLayout Layout,
        string PageSize,
        DocumentKind Kind,
        DocumentContext Context,
        string? CertificateType,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        IReadOnlyList<DocumentSubject> Subjects,
        IReadOnlySet<Guid> AlreadyHeld);

    /// <summary>A card that came out, waiting to be written down.</summary>
    private sealed record Printed(
        Guid Id,
        DocumentSubject Subject,
        string Serial,
        string Key,
        byte[] Pdf);

    public async Task RunAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var work = await StartAsync(organizationId, userId, batchId, cancellationToken);

        if (work is null)
        {
            logger.LogWarning(
                "Document batch {Batch} of organization {Organization} was not found.",
                batchId, organizationId);

            return;
        }

        try
        {
            var (printed, problems, sheet) =
                await ComposeAsync(organizationId, batchId, work, cancellationToken);

            await FinishAsync(
                organizationId, userId, batchId, work, printed, problems, sheet, cancellationToken);
        }
        catch (DocumentBatchRefused refused)
        {
            logger.LogInformation(
                "Document batch {Batch} was refused: {Reason}", batchId, refused.Message);

            await FailAsync(organizationId, userId, batchId, refused.Message, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "Document batch {Batch} could not be printed.", batchId);

            await FailAsync(organizationId, userId, batchId, failure.Message, cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Marks the batch running and reads everything the drawing needs.
    /// </summary>
    /// <remarks>
    /// The layout comes from the archived version rather than from the
    /// template, and that is the point of having an archive: a batch requested
    /// on Monday and printed on Monday evening produces Monday's design, even
    /// if somebody redrew the card in between.
    /// </remarks>
    private async Task<Work?> StartAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await scopes.RunAsync(organizationId, userId, async (_, database) =>
        {
            var batch = await database.DocumentBatches
                .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

            if (batch is null)
            {
                return null;
            }

            batch.Status = DocumentBatchState.Running;

            var version = await database.DocumentTemplateVersions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    candidate => candidate.TemplateId == batch.TemplateId
                        && candidate.Version == batch.TemplateVersion,
                    cancellationToken)
                ?? throw new DocumentBatchRefused(
                    "El diseño con el que se pidió el lote ya no está disponible.");

            var competition = await database.Competitions
                .AsNoTracking()
                .Where(candidate => candidate.Id == batch.CompetitionId)
                .Select(candidate => new { candidate.Name, candidate.Season })
                .SingleAsync(cancellationToken);

            var organization = await database.Organizations
                .AsNoTracking()
                .Where(candidate => candidate.Id == organizationId)
                .Select(candidate => new { candidate.Name, candidate.Slug })
                .SingleAsync(cancellationToken);

            // Everybody registered, which is what both kinds are about today.
            // A certificate issued to a team rather than to a person is what
            // the schema's team_id is for and is not built yet; when it is, it
            // branches here.
            var subjects = await AthletesAsync(database, batch, cancellationToken);

            // Who already holds one of these for this competition. Read as a
            // set rather than asked per subject: a second credential for the
            // same player is not a reprint, it is two valid cards for one
            // person, which is exactly what a credential exists to prevent.
            var held = await database.IssuedDocuments
                .AsNoTracking()
                .Where(document => document.CompetitionId == batch.CompetitionId
                    && document.Kind == batch.Kind
                    && document.Status == DocumentState.Issued
                    && document.AthleteId != null)
                .Select(document => document.AthleteId!.Value)
                .ToListAsync(cancellationToken);

            batch.Total = subjects.Count;

            await database.SaveChangesAsync(cancellationToken);

            return new Work(
                version.Layout,
                version.PageSize,
                batch.Kind,
                new DocumentContext(
                    organization.Name, organization.Slug, competition.Name, competition.Season),
                batch.CertificateType,
                batch.ValidFrom,
                batch.ValidTo,
                subjects,
                held.ToHashSet());
        },
        cancellationToken);

    /// <summary>
    /// Everybody the batch is about.
    /// </summary>
    /// <remarks>
    /// Read through the roster, because that is where the shirt number and the
    /// position live and a credential without them is not much of a
    /// credential. Withdrawn registrations are left out: somebody who left the
    /// team mid-season should not be issued a card that says they play for it.
    /// </remarks>
    private static async Task<List<DocumentSubject>> AthletesAsync(
        SportFrogDbContext database,
        DocumentBatch batch,
        CancellationToken cancellationToken) =>
        await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.Team!.Category!.CompetitionId == batch.CompetitionId)
            .Where(entry => batch.CategoryId == null
                || entry.Team!.CategoryId == batch.CategoryId)
            .Where(entry => batch.TeamId == null || entry.TeamId == batch.TeamId)
            .OrderBy(entry => entry.Team!.Name)
            .ThenBy(entry => entry.Athlete!.LastName)
            .ThenBy(entry => entry.Athlete!.FirstName)
            .Select(entry => new DocumentSubject(
                entry.AthleteId,
                entry.TeamId,
                entry.Athlete!.LastName + " " + entry.Athlete.FirstName,
                entry.Athlete.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.PhotoKey,
                entry.Athlete.DocumentId,
                entry.Athlete.BirthDate,
                entry.JerseyNumber,
                entry.Position,
                entry.Team!.Name,
                entry.Team.Club!.Name,
                entry.Team.Category!.Name))
            .ToListAsync(cancellationToken);

    /// <summary>The slow middle: draw every card, and the sheet they go on.</summary>
    private async Task<(List<Printed> Printed, List<DocumentProblem> Problems, byte[]? Sheet)>
        ComposeAsync(
            Guid organizationId,
            Guid batchId,
            Work work,
            CancellationToken cancellationToken)
    {
        if (work.Subjects.Count == 0)
        {
            throw new DocumentBatchRefused(
                "No hay nadie inscrito que coincida con lo pedido, así que no hay nada que imprimir.");
        }

        // The artwork, once. It is the same on every card of the batch, and
        // fetching it per subject would be four hundred round trips to object
        // storage for four hundred copies of one image.
        var front = await ArtworkAsync(organizationId, work.Layout.Front.BackgroundKey, cancellationToken);
        var back = await ArtworkAsync(organizationId, work.Layout.Back?.BackgroundKey, cancellationToken);

        var wantsPhoto = work.Layout.Front.Fields
            .Concat(work.Layout.Back?.Fields ?? [])
            .Any(field => field.Source == "athlete.photo");

        var printed = new List<Printed>(work.Subjects.Count);
        var problems = new List<DocumentProblem>();
        var cards = new List<SheetCard>(work.Subjects.Count);
        var serials = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subject in work.Subjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (subject.AthleteId is { } athleteId && work.AlreadyHeld.Contains(athleteId))
            {
                problems.Add(new DocumentProblem(subject.Label, SkipReason.AlreadyIssued));
                continue;
            }

            // A design that prints a face, for somebody with no face on file.
            // Skipped rather than printed with a hole: a credential without a
            // photograph does not do the one thing a credential is for, and
            // handing one over would be worse than saying who is missing.
            if (wantsPhoto && !HasPhoto(subject.PhotoKey))
            {
                problems.Add(new DocumentProblem(subject.Label, SkipReason.NoPhoto));
                continue;
            }

            var serial = NextSerial(serials);
            var documentId = Guid.NewGuid();

            var print = new DocumentPrint(
                subject,
                work.Context,
                serial,
                VerificationCode.Address(
                    options.Value.VerificationBaseUrl, work.Context.OrganizationSlug, serial),
                work.CertificateType,
                work.ValidFrom,
                work.ValidTo,
                DateTimeOffset.UtcNow);

            try
            {
                var assets = new DocumentAssets(
                    front,
                    back,
                    await PhotoAsync(organizationId, subject.PhotoKey, cancellationToken),
                    VerificationCode.Draw(print.VerifyUrl));

                var pdf = DocumentRenderer.Render(work.Layout, work.PageSize, print, assets);

                printed.Add(new Printed(
                    documentId,
                    subject,
                    serial,
                    StorageKeys.IssuedDocument(organizationId, documentId),
                    pdf));

                cards.Add(new SheetCard(work.Layout.Front, print, assets));
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // One card that would not compose must not cost the other
                // three hundred and ninety-nine. Named, so somebody can find
                // out why theirs is missing.
                logger.LogWarning(
                    failure, "Could not compose a document for {Subject}.", subject.Label);

                problems.Add(new DocumentProblem(
                    subject.Label, SkipReason.CouldNotPrint, failure.Message));
            }
        }

        foreach (var card in printed)
        {
            await store.PutAsync(card.Key, card.Pdf, "application/pdf", cancellationToken);
        }

        var page = TemplateDesign.PageSizes.First(size => size.Code == work.PageSize);
        byte[]? sheet = null;

        // Only where ganging up means anything. A certificate is already a
        // sheet of paper; laying two on a page would print them at half size,
        // which is not an imposition, it is a mistake.
        if (cards.Count > 0 && PrintSheet.Fits(page))
        {
            sheet = PrintSheet.Render(page, cards);

            await store.PutAsync(
                StorageKeys.DocumentSheet(organizationId, batchId),
                sheet,
                "application/pdf",
                cancellationToken);
        }

        return (printed, problems, sheet);
    }

    /// <summary>
    /// A serial nothing else in this batch is using.
    /// </summary>
    /// <remarks>
    /// Sixty bits, so a collision inside one batch is not something that
    /// happens; the check costs nothing and means the card is never printed
    /// with a code the row will then be refused for. Across batches the unique
    /// index is the authority, and a collision there fails the batch — at odds
    /// nobody will ever see.
    /// </remarks>
    private static string NextSerial(HashSet<string> used)
    {
        while (true)
        {
            var serial = Serial.Next();

            if (used.Add(serial))
            {
                return serial;
            }
        }
    }

    /// <summary>
    /// Whether there is a photograph the generator can actually draw.
    /// </summary>
    /// <remarks>
    /// A photograph uploaded before images moved out of the database is still
    /// sitting in the column as a data URL. It is a real picture and the
    /// interface shows it, but nothing here can fetch it from the bucket — so
    /// for the purposes of printing there is no photograph, and saying so is
    /// better than printing a card with a hole in it. Correcting the athlete
    /// converts it.
    /// </remarks>
    private static bool HasPhoto(string? key) =>
        !string.IsNullOrEmpty(key)
        && !key.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    private async Task<byte[]?> ArtworkAsync(
        Guid organizationId,
        string? key,
        CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key)
            ? null
            : await store.ReadAsync(organizationId, key, cancellationToken);

    /// <summary>
    /// A subject's photograph, or nothing if it cannot be read.
    /// </summary>
    /// <remarks>
    /// A missing object is not a reason to fail the card here — the batch has
    /// already decided that a design wanting a photograph skips anybody
    /// without one, and this is the narrower case of a key that points at
    /// something the bucket has lost. The card comes out with a blank where
    /// the face goes and the operator sees a document they can look at.
    /// </remarks>
    private async Task<byte[]?> PhotoAsync(
        Guid organizationId,
        string? key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key) || key.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return await store.ReadAsync(organizationId, key, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Could not read the photograph {Key}.", key);

            return null;
        }
    }

    /// <summary>Writes down every card that came out.</summary>
    private async Task FinishAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        Work work,
        List<Printed> printed,
        List<DocumentProblem> problems,
        byte[]? sheet,
        CancellationToken cancellationToken) =>
        await scopes.RunAsync(organizationId, userId, async (_, database) =>
        {
            var batch = await database.DocumentBatches.SingleAsync(
                candidate => candidate.Id == batchId, cancellationToken);

            foreach (var card in printed)
            {
                database.IssuedDocuments.Add(new IssuedDocument
                {
                    Id = card.Id,
                    OrgId = organizationId,
                    TemplateId = batch.TemplateId,
                    TemplateVersion = batch.TemplateVersion,
                    Kind = work.Kind,
                    CompetitionId = batch.CompetitionId,
                    AthleteId = card.Subject.AthleteId,
                    TeamId = card.Subject.TeamId,
                    SerialNumber = card.Serial,
                    CertificateType = work.CertificateType,
                    ValidFrom = work.ValidFrom,
                    ValidTo = work.ValidTo,
                    PdfUrl = card.Key,
                    IssuedBy = userId,
                    BatchId = batchId,
                });
            }

            batch.Issued = printed.Count;
            batch.Skipped = problems.Count;
            batch.Problems = problems;
            batch.SheetKey = sheet is null ? null : StorageKeys.DocumentSheet(organizationId, batchId);
            batch.Status = DocumentBatchState.Finished;
            batch.FinishedAt = DateTimeOffset.UtcNow;

            await database.SaveChangesAsync(cancellationToken);

            return true;
        },
        cancellationToken);

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
                var batch = await database.DocumentBatches.SingleOrDefaultAsync(
                    candidate => candidate.Id == batchId, cancellationToken);

                if (batch is not null)
                {
                    batch.Status = DocumentBatchState.Failed;
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
            logger.LogError(
                writing, "Could not record the failure of document batch {Batch}.", batchId);
        }
    }
}
