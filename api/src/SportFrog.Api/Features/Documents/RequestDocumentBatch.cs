using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Asks for a batch of documents and answers straight away.
/// </summary>
/// <remarks>
/// What it pins at this moment, rather than looking up when the worker gets
/// round to it, differs by kind. A certificate pins the design it prints
/// from: a batch requested on Monday afternoon and printed on Monday evening
/// produces the card that was on screen when somebody pressed the button, not
/// whatever the design became while they were at dinner. A credential has no
/// design to pin — its structure never changes — so it pins the accreditation
/// catalogue and the organization's legal notice instead, for the same
/// reason: see <see cref="CredentialSnapshot"/>'s own remarks.
/// </remarks>
public static class RequestDocumentBatch
{
    /// <param name="TemplateId">
    /// Required for a certificate, which prints from a design; absent for a
    /// credential, which has none.
    /// </param>
    public sealed record Request(
        string Kind,
        Guid? TemplateId,
        Guid CompetitionId,
        Guid? CategoryId = null,
        Guid? TeamId = null,
        string? CertificateType = null,
        DateOnly? ValidFrom = null,
        DateOnly? ValidTo = null);

    public sealed record Response(Guid Id, DocumentBatchState Status);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Kind)
                .Must(kind => WireEnum.TryParse<DocumentKind>(kind, out _))
                .WithMessage($"Tipo desconocido. Disponibles: {WireEnum.Options<DocumentKind>()}.");

            RuleFor(request => request.TemplateId)
                .NotEmpty().WithMessage("El diseño es obligatorio.")
                .When(request => IsCertificate(request.Kind));

            RuleFor(request => request.CompetitionId)
                .NotEmpty().WithMessage("La competencia es obligatoria.");

            RuleFor(request => request.CertificateType)
                .MaximumLength(80)
                .When(request => request.CertificateType is not null);

            RuleFor(request => request)
                .Must(request => request.ValidFrom is null
                    || request.ValidTo is null
                    || request.ValidFrom <= request.ValidTo)
                .WithMessage("La vigencia empieza después de terminar.")
                .WithName("ValidFrom");
        }

        private static bool IsCertificate(string kind) =>
            WireEnum.TryParse<DocumentKind>(kind, out var parsed) && parsed == DocumentKind.Certificate;
    }

    public static IEndpointRouteBuilder MapRequestDocumentBatch(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/documents/batches", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RequestDocumentBatch))
            .WithSummary("Asks for a batch of credentials or certificates to be printed.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationContext organization,
        IBackgroundJobClient jobs,
        CancellationToken cancellationToken)
    {
        var kind = WireEnum.Parse<DocumentKind>(request.Kind);

        var competition = await database.Competitions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.CompetitionId, cancellationToken);

        if (competition is null)
        {
            return Refuse("competitionId", "No hay ninguna competencia de esta organización con ese identificador.");
        }

        if (request.CategoryId is { } categoryId && !await database.Categories.AnyAsync(
                candidate => candidate.Id == categoryId
                    && candidate.CompetitionId == request.CompetitionId,
                cancellationToken))
        {
            return Refuse("categoryId", "Esa categoría no es de esta competencia.");
        }

        if (request.TeamId is { } teamId && !await database.Teams.AnyAsync(
                candidate => candidate.Id == teamId
                    && candidate.Category!.CompetitionId == request.CompetitionId,
                cancellationToken))
        {
            return Refuse("teamId", "Ese equipo no es de esta competencia.");
        }

        DocumentTemplate? template = null;
        CredentialSnapshot? snapshot = null;

        if (kind == DocumentKind.Certificate)
        {
            if (string.IsNullOrWhiteSpace(request.CertificateType))
            {
                // A certificate says why it was given. One that does not is a
                // sheet of paper with somebody's name on it.
                return Refuse("certificateType", "Un certificado tiene que decir por qué se otorga.");
            }

            var resolved = await ResolveTemplateAsync(database, request.TemplateId!.Value, kind, cancellationToken);

            if (resolved.Refusal is not null)
            {
                return resolved.Refusal;
            }

            template = resolved.Template;
        }
        else
        {
            var resolved = await BuildSnapshotAsync(database, competition, cancellationToken);

            if (resolved.Refusal is not null)
            {
                return resolved.Refusal;
            }

            snapshot = resolved.Snapshot;
        }

        var organizationId = organization.RequireOrganizationId();

        var batch = new DocumentBatch
        {
            Id = Guid.NewGuid(),
            OrgId = organizationId,
            Kind = kind,

            // Pinned now, one way or the other — this is the whole reason
            // the template archive and the credential snapshot exist.
            TemplateId = template?.Id,
            TemplateVersion = template?.Version,
            CredentialSnapshot = snapshot,

            CompetitionId = request.CompetitionId,
            CategoryId = request.CategoryId,
            TeamId = request.TeamId,
            CertificateType = request.CertificateType?.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RequestedBy = organization.UserId ?? Guid.Empty,
        };

        database.DocumentBatches.Add(batch);
        await database.SaveChangesAsync(cancellationToken);

        // Once the response has actually gone out, so the job cannot start
        // before the row it is about is visible. The photo import explains
        // this at length; the hazard is identical.
        context.Response.OnCompleted(() =>
        {
            jobs.Enqueue<IssueDocumentsJob>(job =>
                job.RunAsync(organizationId, batch.RequestedBy, batch.Id, CancellationToken.None));

            return Task.CompletedTask;
        });

        return Results.Accepted(
            $"/documents/batches/{batch.Id}", new Response(batch.Id, DocumentBatchState.Queued));
    }

    private static async Task<(IResult? Refusal, DocumentTemplate? Template)> ResolveTemplateAsync(
        SportFrogDbContext database, Guid templateId, DocumentKind kind, CancellationToken cancellationToken)
    {
        var template = await database.DocumentTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == templateId, cancellationToken);

        if (template is null)
        {
            return (Refuse("templateId", "No hay ningún diseño de esta organización con ese identificador."), null);
        }

        if (template.Kind != kind)
        {
            // Printing certificates from a credential design would produce
            // cards with blank spaces where the fields do not apply, and the
            // discovery would be a stack of them.
            return (Refuse(
                "templateId",
                $"Ese diseño es de tipo {WireEnum.Label(template.Kind)}, no {WireEnum.Label(kind)}."), null);
        }

        return (null, template);
    }

    /// <summary>
    /// The accreditation catalogue and legal notice, exactly as they stand
    /// right now, ready to be pinned onto the batch.
    /// </summary>
    /// <remarks>
    /// Refused when the competition has no accreditation categories at all:
    /// a credential with no category to hold is not a smaller version of a
    /// credential, it is nothing to print, the same reasoning
    /// <see cref="IssueDocumentsJob"/> applies per subject when one person
    /// has not been accredited — this is that same check, at the level of
    /// the whole competition, before a batch is even accepted.
    /// </remarks>
    internal static async Task<(IResult? Refusal, CredentialSnapshot? Snapshot)> BuildSnapshotAsync(
        SportFrogDbContext database, Competition competition, CancellationToken cancellationToken)
    {
        var categories = await database.AccreditationCategories
            .AsNoTracking()
            .Where(category => category.CompetitionId == competition.Id)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
        {
            return (Refuse(
                "competitionId",
                "Esta competencia todavía no tiene categorías de acreditación. Cargá el catálogo antes " +
                "de pedir un lote de credenciales."), null);
        }

        var items = await database.AccreditationItems
            .AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .ToListAsync(cancellationToken);

        var design = await CredentialDesignChoice.LoadForAsync(competition.CredentialDesignId, database, cancellationToken);

        // Frozen here, along with the catalogue, so reprinting this batch
        // later produces the card it produced now. Only the competition's own
        // logo, the design's values and the portal's fallbacks are read — see
        // CredentialDesignResolver for which owns what.
        var resolution = CredentialDesignResolver.Resolve(
            competition.Settings.Public, CredentialDesignChoice.ToValues(design));

        var snapshot = new CredentialSnapshot(
            CompetitionLogoKey: resolution.LogoKey,
            AccentColorHex: resolution.AccentColorHex,
            LegalText: resolution.LegalText,
            Items: items.ToDictionary(
                item => item.Id,
                item => new CredentialSnapshotItem(item.Kind, item.Code, item.Name, item.ColorHex, item.IconKey)),
            Categories: categories.ToDictionary(
                category => category.Id,
                category => new CredentialSnapshotCategory(category.Code, category.Name, category.ColorHex)),
            BackgroundKey: resolution.BackgroundKey);

        return (null, snapshot);
    }

    private static IResult Refuse(string property, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [property] = [message],
        });
}
