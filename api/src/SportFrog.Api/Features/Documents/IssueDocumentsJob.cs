using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Features.Athletes;
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
/// The heaviest work in the system, and the reason the queue exists. A
/// certificate decodes a photograph, draws a layout over artwork and builds a
/// QR code; a credential does the same plus resolves an accreditation and
/// claims a visible number — four hundred of either is minutes, and no
/// browser waits.
///
/// Three parts, like every job here, for each kind: reading what to print is
/// one transaction, the drawing holds no transaction at all, and recording
/// what was printed is a third. The middle part is where the minutes go, and
/// holding a database transaction across it would be a long-running lock
/// bought for nothing.
///
/// The two kinds no longer share a drawing path — see
/// <see cref="CredentialRenderer"/>'s own remarks for why — so they do not
/// share a reading or a writing path either, below <see cref="StartAsync"/>
/// and the single try/catch in <see cref="RunAsync"/> that both still answer
/// to.
/// </remarks>
public sealed class IssueDocumentsJob(
    OrganizationJobScope scopes,
    ObjectStore store,
    AthletePhoto photos,
    IOptions<DocumentOptions> options,
    ILogger<IssueDocumentsJob> logger)
{
    private abstract record Work(IReadOnlySet<Guid> AlreadyHeld);

    /// <summary>Everything drawing a batch of certificates needs, read in one go.</summary>
    private sealed record CertificateWork(
        TemplateLayout Layout,
        string PageSize,
        DocumentContext Context,
        string? CertificateType,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        IReadOnlyList<DocumentSubject> Subjects,
        IReadOnlySet<Guid> AlreadyHeld) : Work(AlreadyHeld);

    /// <summary>
    /// One person's print-ready facts — the credential's counterpart to
    /// <see cref="DocumentSubject"/>.
    /// </summary>
    /// <param name="AccreditationId">
    /// Null when this person is on the roster but nobody has assigned them an
    /// accreditation category yet — the compose step records that as
    /// <see cref="SkipReason.NotAccredited"/> rather than failing the batch.
    /// </param>
    /// <param name="Grants">
    /// Null exactly when <paramref name="AccreditationId"/> is — present and
    /// possibly empty otherwise, which is the ordinary case of a category
    /// that genuinely grants nothing yet.
    /// </param>
    private sealed record CredentialSubjectWork(
        Guid AthleteId,
        Guid TeamId,
        string Label,
        string FullName,
        string? PhotoKey,
        string ClubName,
        string? ClubShortName,
        string? ClubLogoKey,
        Guid? AccreditationId,
        string? CategoryCode,
        string? CategoryName,
        string? CategoryColorHex,
        IReadOnlyList<CredentialGrant>? Grants,
        string? DocumentId = null);

    /// <summary>Everything drawing a batch of credentials needs, read in one go.</summary>
    private sealed record CredentialWork(
        Guid CompetitionId,
        string OrganizationSlug,
        CredentialContext Context,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        IReadOnlyList<CredentialSubjectWork> Subjects,
        IReadOnlySet<Guid> AlreadyHeld) : Work(AlreadyHeld);

    /// <summary>A certificate that came out, waiting to be written down.</summary>
    private sealed record Printed(
        Guid Id,
        DocumentSubject Subject,
        string Serial,
        string Key,
        byte[] Pdf);

    /// <summary>A credential that came out, waiting to be written down.</summary>
    private sealed record CredentialPrinted(
        Guid Id,
        Guid AthleteId,
        Guid TeamId,
        string VisibleId,
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
            switch (work)
            {
                case CertificateWork certificate:
                {
                    var (printed, problems, sheet) =
                        await ComposeCertificatesAsync(organizationId, batchId, certificate, cancellationToken);

                    await FinishCertificatesAsync(
                        organizationId, userId, batchId, certificate, printed, problems, sheet, cancellationToken);

                    break;
                }

                case CredentialWork credential:
                {
                    var (printed, problems, batchPdf) =
                        await ComposeCredentialsAsync(organizationId, userId, batchId, credential, cancellationToken);

                    await FinishCredentialsAsync(
                        organizationId, userId, batchId, credential, printed, problems, batchPdf, cancellationToken);

                    break;
                }
            }
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
    /// Marks the batch running and reads everything the drawing needs, for
    /// whichever kind the batch turns out to be.
    /// </summary>
    private async Task<Work?> StartAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await scopes.RunAsync(organizationId, userId, async (services, database) =>
        {
            var batch = await database.DocumentBatches
                .SingleOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

            if (batch is null)
            {
                return null;
            }

            batch.Status = DocumentBatchState.Running;

            Work work = batch.Kind == DocumentKind.Credential
                // The resolver comes from this scope, not from the job: its own
                // reads are protected by row-level security, and a resolver built
                // outside an organization reads every grant as absent, which
                // prints a card with no zones, no services and no glossary.
                ? await StartCredentialAsync(
                    organizationId, batch, database,
                    services.GetRequiredService<AccreditationResolver>(), cancellationToken)
                : await StartCertificateAsync(organizationId, batch, database, cancellationToken);

            batch.Total = work switch
            {
                CertificateWork certificate => certificate.Subjects.Count,
                CredentialWork credential => credential.Subjects.Count,
                _ => 0,
            };

            await database.SaveChangesAsync(cancellationToken);

            return work;
        },
        cancellationToken);

    /// <summary>
    /// Reads a certificate batch's design, context and subjects.
    /// </summary>
    /// <remarks>
    /// The layout comes from the archived version rather than from the
    /// template, and that is the point of having an archive: a batch requested
    /// on Monday and printed on Monday evening produces Monday's design, even
    /// if somebody redrew the card in between.
    /// </remarks>
    private static async Task<CertificateWork> StartCertificateAsync(
        Guid organizationId, DocumentBatch batch, SportFrogDbContext database, CancellationToken cancellationToken)
    {
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

        var subjects = await CertificateSubjectsAsync(database, batch, cancellationToken);
        var held = await HeldAthleteIdsAsync(database, batch, cancellationToken);

        return new CertificateWork(
            version.Layout,
            version.PageSize,
            new DocumentContext(
                organization.Name, organization.Slug, competition.Name, competition.Season),
            batch.CertificateType,
            batch.ValidFrom,
            batch.ValidTo,
            subjects,
            held);
    }

    /// <summary>
    /// Reads a credential batch's frozen catalogue, context and subjects —
    /// everybody on the competition's active roster, each with whatever
    /// accreditation they currently hold.
    /// </summary>
    /// <remarks>
    /// The catalogue's codes, names and colours come from
    /// <see cref="DocumentBatch.CredentialSnapshot"/>, frozen when the batch
    /// was requested — see its own remarks for why. Who holds which category
    /// is read fresh here instead, the same way <see cref="CertificateSubjectsAsync"/>
    /// reads the roster fresh rather than from anything pinned: an
    /// accreditation is data about a person, not part of the card's design.
    /// </remarks>
    private static async Task<CredentialWork> StartCredentialAsync(
        Guid organizationId,
        DocumentBatch batch,
        SportFrogDbContext database,
        AccreditationResolver resolver,
        CancellationToken cancellationToken)
    {
        var snapshot = batch.CredentialSnapshot
            ?? throw new DocumentBatchRefused(
                "Este lote de credenciales no tiene un catálogo guardado y no se puede imprimir.");

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

        var context = new CredentialContext(
            organization.Name, competition.Name, competition.Season,
            snapshot.CompetitionLogoKey, snapshot.AccentColorHex, snapshot.LegalText, snapshot.BackgroundKey);

        var roster = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.Team!.Category!.CompetitionId == batch.CompetitionId)
            .Where(entry => batch.CategoryId == null
                || entry.Team!.CategoryId == batch.CategoryId)
            .Where(entry => batch.TeamId == null || entry.TeamId == batch.TeamId)
            .OrderBy(entry => entry.Team!.Name)
            .ThenBy(entry => entry.Athlete!.LastName)
            .ThenBy(entry => entry.Athlete!.FirstName)
            .Select(entry => new
            {
                entry.AthleteId,
                entry.TeamId,
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.PhotoKey,
                entry.Athlete.DocumentId,
                ClubName = entry.Team!.Club!.Name,
                ClubShortName = entry.Team.Club.ShortName,
                ClubLogoKey = entry.Team.Club.LogoUrl,
            })
            .ToListAsync(cancellationToken);

        var athleteIds = roster.Select(entry => entry.AthleteId).ToList();

        var accreditations = await database.AthleteAccreditations
            .AsNoTracking()
            .Where(accreditation => accreditation.CompetitionId == batch.CompetitionId
                && athleteIds.Contains(accreditation.AthleteId))
            .Select(accreditation => new { accreditation.Id, accreditation.AthleteId, accreditation.CategoryId })
            .ToListAsync(cancellationToken);

        var accreditationByAthlete = accreditations.ToDictionary(accreditation => accreditation.AthleteId);

        // One round trip for every subject's resolved grants, not one per
        // subject — see AccreditationResolver.ResolveManyAsync's own remarks.
        var sourcesByAccreditation = await resolver.ResolveManyAsync(
            accreditations.Select(accreditation => accreditation.Id).ToList(), cancellationToken);

        var subjects = roster
            .Select(entry => BuildCredentialSubject(entry.AthleteId, entry.TeamId, entry.FirstName, entry.LastName,
                entry.PhotoKey, entry.DocumentId, entry.ClubName, entry.ClubShortName, entry.ClubLogoKey,
                accreditationByAthlete.GetValueOrDefault(entry.AthleteId)?.Id,
                accreditationByAthlete.TryGetValue(entry.AthleteId, out var accreditation) ? accreditation.CategoryId : null,
                snapshot, sourcesByAccreditation))
            .ToList();

        var held = await HeldAthleteIdsAsync(database, batch, cancellationToken);

        return new CredentialWork(
            batch.CompetitionId, organization.Slug, context, batch.ValidFrom, batch.ValidTo, subjects, held);
    }

    private static CredentialSubjectWork BuildCredentialSubject(
        Guid athleteId,
        Guid teamId,
        string firstName,
        string lastName,
        string? photoKey,
        string? documentId,
        string clubName,
        string? clubShortName,
        string? clubLogoKey,
        Guid? accreditationId,
        Guid? categoryId,
        CredentialSnapshot snapshot,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, AccreditationItemSource>> sourcesByAccreditation)
    {
        var label = $"{lastName} {firstName}";
        var fullName = $"{firstName} {lastName}";

        // Not accredited at all, or accredited under a category this
        // snapshot does not have — the second is only reachable for a batch
        // reissued against an older snapshot than a category's own later
        // history; the catalogue itself cannot delete a category anybody
        // still holds (athlete_accreditations.category_id restricts that).
        // Either way, there is nothing frozen to draw.
        if (accreditationId is not { } id
            || categoryId is not { } category
            || !snapshot.Categories.TryGetValue(category, out var snapshotCategory))
        {
            return new CredentialSubjectWork(
                athleteId, teamId, label, fullName, photoKey, clubName, clubShortName, clubLogoKey,
                null, null, null, null, null, documentId);
        }

        var sources = sourcesByAccreditation.GetValueOrDefault(
            id, new Dictionary<Guid, AccreditationItemSource>());

        var grants = sources.Keys
            .Where(snapshot.Items.ContainsKey)
            .Select(itemId =>
            {
                var item = snapshot.Items[itemId];
                return new CredentialGrant(itemId, item.Kind, item.Code, item.Name, item.ColorHex, item.IconKey);
            })
            .ToList();

        return new CredentialSubjectWork(
            athleteId, teamId, label, fullName, photoKey, clubName, clubShortName, clubLogoKey,
            id, snapshotCategory.Code, snapshotCategory.Name, snapshotCategory.ColorHex, grants, documentId);
    }

    /// <summary>
    /// Everybody a certificate batch is about.
    /// </summary>
    /// <remarks>
    /// Read through the roster, because that is where the shirt number and the
    /// position live and a credential without them is not much of a
    /// credential. Withdrawn registrations are left out: somebody who left the
    /// team mid-season should not be issued a card that says they play for it.
    /// </remarks>
    private static async Task<List<DocumentSubject>> CertificateSubjectsAsync(
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

    /// <summary>
    /// Who already holds a valid document of this batch's kind for this
    /// competition.
    /// </summary>
    /// <remarks>
    /// Read as a set rather than asked per subject: a second document for the
    /// same player is not a reprint, it is two valid documents for one
    /// person, which is exactly what both kinds exist to prevent.
    /// </remarks>
    private static async Task<IReadOnlySet<Guid>> HeldAthleteIdsAsync(
        SportFrogDbContext database, DocumentBatch batch, CancellationToken cancellationToken) =>
        (await database.IssuedDocuments
            .AsNoTracking()
            .Where(document => document.CompetitionId == batch.CompetitionId
                && document.Kind == batch.Kind
                && document.Status == DocumentState.Issued
                && document.AthleteId != null)
            .Select(document => document.AthleteId!.Value)
            .ToListAsync(cancellationToken))
            .ToHashSet();

    /// <summary>The slow middle: draw every certificate, and the sheet they go on.</summary>
    private async Task<(List<Printed> Printed, List<DocumentProblem> Problems, byte[]? Sheet)>
        ComposeCertificatesAsync(
            Guid organizationId,
            Guid batchId,
            CertificateWork work,
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
                    await photos.BytesAsync(organizationId, subject.PhotoKey, cancellationToken),
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
    /// The slow middle for a credential batch: resolve, number, draw every
    /// card, and the one PDF they all go into.
    /// </summary>
    /// <remarks>
    /// Concatenated into a single batch PDF rather than imposed onto a sheet
    /// — see <see cref="CredentialRenderer.RenderBatch"/>'s own remarks for
    /// why a credential has no smaller unit to gang several of across a
    /// bigger page the way <see cref="PrintSheet"/> does for a certificate.
    /// </remarks>
    private async Task<(List<CredentialPrinted> Printed, List<DocumentProblem> Problems, byte[]? BatchPdf)>
        ComposeCredentialsAsync(
            Guid organizationId,
            Guid userId,
            Guid batchId,
            CredentialWork work,
            CancellationToken cancellationToken)
    {
        if (work.Subjects.Count == 0)
        {
            throw new DocumentBatchRefused(
                "No hay nadie inscrito que coincida con lo pedido, así que no hay nada que imprimir.");
        }

        // The competition's own logo, once — shared by every credential of
        // the batch. Club logos are cached per club rather than fetched once
        // overall, since a batch ordinarily spans several clubs but usually
        // repeats each one across many of its players.
        var competitionLogo = await ArtworkAsync(organizationId, work.Context.CompetitionLogoKey, cancellationToken);

        // The background, once and already faded, shared by every card of the
        // batch for the same reason as the logo above.
        var background = CredentialWatermark.Fade(
            await ArtworkAsync(organizationId, work.Context.BackgroundKey, cancellationToken));

        var clubLogoCache = new Dictionary<string, byte[]?>();

        var printed = new List<CredentialPrinted>(work.Subjects.Count);
        var problems = new List<DocumentProblem>();
        var sheets = new List<(CredentialPrint Print, CredentialAssets Assets)>(work.Subjects.Count);
        var serials = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subject in work.Subjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (work.AlreadyHeld.Contains(subject.AthleteId))
            {
                problems.Add(new DocumentProblem(subject.Label, SkipReason.AlreadyIssued));
                continue;
            }

            if (subject.AccreditationId is null || subject.Grants is null)
            {
                problems.Add(new DocumentProblem(subject.Label, SkipReason.NotAccredited));
                continue;
            }

            var serial = NextSerial(serials);
            var documentId = Guid.NewGuid();

            try
            {
                var visibleId = await ClaimVisibleIdAsync(
                    organizationId, userId, work.CompetitionId, subject, cancellationToken);

                var print = new CredentialPrint(
                    new CredentialSubject(subject.FullName, subject.PhotoKey, subject.ClubName, subject.ClubLogoKey, subject.DocumentId),
                    work.Context,
                    subject.CategoryCode!,
                    subject.CategoryName!,
                    subject.CategoryColorHex!,
                    visibleId,
                    VerificationCode.Address(options.Value.VerificationBaseUrl, work.OrganizationSlug, serial),
                    subject.Grants,
                    DateTimeOffset.UtcNow);

                var assets = new CredentialAssets(
                    await photos.BytesAsync(organizationId, subject.PhotoKey, cancellationToken),
                    await ClubLogoAsync(subject.ClubLogoKey, clubLogoCache, organizationId, cancellationToken),
                    competitionLogo,
                    VerificationCode.Draw(print.VerifyUrl),
                    Background: background);

                var pdf = CredentialRenderer.Render(print, assets);

                printed.Add(new CredentialPrinted(
                    documentId,
                    subject.AthleteId,
                    subject.TeamId,
                    visibleId,
                    serial,
                    StorageKeys.IssuedDocument(organizationId, documentId),
                    pdf));

                sheets.Add((print, assets));
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // One card that would not compose must not cost the rest of
                // the batch. Named, so somebody can find out why theirs is
                // missing.
                logger.LogWarning(
                    failure, "Could not compose a credential for {Subject}.", subject.Label);

                problems.Add(new DocumentProblem(
                    subject.Label, SkipReason.CouldNotPrint, failure.Message));
            }
        }

        foreach (var card in printed)
        {
            await store.PutAsync(card.Key, card.Pdf, "application/pdf", cancellationToken);
        }

        byte[]? batchPdf = null;

        if (sheets.Count > 0)
        {
            batchPdf = CredentialRenderer.RenderBatch(sheets);

            await store.PutAsync(
                StorageKeys.DocumentSheet(organizationId, batchId),
                batchPdf,
                "application/pdf",
                cancellationToken);
        }

        return (printed, problems, batchPdf);
    }

    private async Task<byte[]?> ClubLogoAsync(
        string? key, Dictionary<string, byte[]?> cache, Guid organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bytes = await ArtworkAsync(organizationId, key, cancellationToken);
        cache[key] = bytes;

        return bytes;
    }

    /// <summary>
    /// Claims the next visible number for a credential, in a scope of its own
    /// with the organization set.
    /// </summary>
    /// <remarks>
    /// The counter is protected by row-level security, and an insert from a
    /// connection with no organization set is refused. The drawing runs
    /// outside any transaction, so the claim has to open its own, the same way
    /// every other write of this job does.
    ///
    /// The claim commits on its own. A card that then fails to draw leaves a
    /// gap in the sequence. That is the price of a number that nobody else can
    /// claim at the same instant, and a gap is easier to explain at a gate
    /// than a number printed twice.
    /// </remarks>
    private Task<string> ClaimVisibleIdAsync(
        Guid organizationId,
        Guid userId,
        Guid competitionId,
        CredentialSubjectWork subject,
        CancellationToken cancellationToken) =>
        scopes.RunAsync(organizationId, userId, (services, _) =>
            services.GetRequiredService<CredentialNumbering>().NextAsync(
                organizationId, competitionId, subject.ClubShortName, subject.ClubName, cancellationToken),
            cancellationToken);

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

    private async Task<byte[]?> ArtworkAsync(
        Guid organizationId,
        string? key,
        CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key)
            ? null
            : await store.ReadAsync(organizationId, key, cancellationToken);

    // A subject's photograph, or the placeholder silhouette if there isn't
    // one to draw, is now AthletePhoto.BytesAsync — shared with
    // Features.Reports.AthleteReportQuery, which answers the exact same
    // question for a different document. See its own remarks for the three
    // cases that draw the placeholder instead of failing the card.

    /// <summary>Writes down every certificate that came out.</summary>
    private async Task FinishCertificatesAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CertificateWork work,
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
                    Kind = DocumentKind.Certificate,
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

    /// <summary>Writes down every credential that came out.</summary>
    private async Task FinishCredentialsAsync(
        Guid organizationId,
        Guid userId,
        Guid batchId,
        CredentialWork work,
        List<CredentialPrinted> printed,
        List<DocumentProblem> problems,
        byte[]? batchPdf,
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
                    TemplateId = null,
                    TemplateVersion = null,
                    Kind = DocumentKind.Credential,
                    CompetitionId = batch.CompetitionId,
                    AthleteId = card.AthleteId,
                    TeamId = card.TeamId,
                    SerialNumber = card.Serial,
                    VisibleId = card.VisibleId,
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
            batch.SheetKey = batchPdf is null ? null : StorageKeys.DocumentSheet(organizationId, batchId);
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
