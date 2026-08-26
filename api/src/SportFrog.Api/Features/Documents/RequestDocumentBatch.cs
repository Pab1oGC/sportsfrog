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
/// The design it will print from is pinned here, at the moment of asking,
/// rather than looked up when the worker gets round to it. A batch requested
/// on Monday afternoon and printed on Monday evening produces the card that
/// was on screen when somebody pressed the button — not whatever the design
/// became while they were at dinner.
/// </remarks>
public static class RequestDocumentBatch
{
    public sealed record Request(
        string Kind,
        Guid TemplateId,
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
                .NotEmpty().WithMessage("El diseño es obligatorio.");

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

        var template = await database.DocumentTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Refuse("templateId", "No hay ningún diseño de esta organización con ese identificador.");
        }

        if (template.Kind != kind)
        {
            // Printing certificates from a credential design would produce
            // cards with blank spaces where the fields do not apply, and the
            // discovery would be a stack of them.
            return Refuse(
                "templateId",
                $"Ese diseño es de tipo {WireEnum.Label(template.Kind)}, no {WireEnum.Label(kind)}.");
        }

        if (!await database.Competitions
                .AnyAsync(candidate => candidate.Id == request.CompetitionId, cancellationToken))
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

        if (kind == DocumentKind.Certificate && string.IsNullOrWhiteSpace(request.CertificateType))
        {
            // A certificate says why it was given. One that does not is a
            // sheet of paper with somebody's name on it.
            return Refuse("certificateType", "Un certificado tiene que decir por qué se otorga.");
        }

        var organizationId = organization.RequireOrganizationId();

        var batch = new DocumentBatch
        {
            Id = Guid.NewGuid(),
            OrgId = organizationId,
            Kind = kind,
            TemplateId = template.Id,

            // Pinned now. This is the whole reason the archive exists.
            TemplateVersion = template.Version,
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

    private static IResult Refuse(string property, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [property] = [message],
        });
}
