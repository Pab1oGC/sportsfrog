using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Asks SportFrog to print a batch of documents, on behalf of an
/// organization that may no longer print them itself.
/// </summary>
/// <remarks>
/// The organization-facing half of the gate <see cref="RequestDocumentBatch"/>
/// closes to everyone but the platform organization (see
/// <see cref="PlatformAuthority"/>): this is what is left for an operator to
/// do instead of requesting the batch directly — for a credential, which
/// never had a design of its own to ask for, and now for a certificate too,
/// since <see cref="CreateTemplate"/> closed that door the same way. It
/// holds the same facts a document batch needs and none of the authority to
/// start one — approving it, not raising it, is what will create the
/// <see cref="DocumentBatch"/>.
/// </remarks>
public static class RequestDocumentRequest
{
    /// <param name="TemplateId">
    /// Required for a certificate, which prints from a design; refused for a
    /// credential, which has none — the same two-way rule
    /// <see cref="RequestDocumentBatch.Request"/> enforces for a batch.
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

    public sealed record Response(Guid Id, DocumentRequestState Status);

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

            RuleFor(request => request.TemplateId)
                .Empty().WithMessage("Una credencial no tiene un diseño que nombrar.")
                .When(request => !IsCertificate(request.Kind));

            RuleFor(request => request.CompetitionId)
                .NotEmpty().WithMessage("La competencia es obligatoria.");

            RuleFor(request => request.CertificateType)
                .NotEmpty().WithMessage("Un certificado tiene que decir por qué se otorga.")
                .MaximumLength(80)
                .When(request => IsCertificate(request.Kind));

            RuleFor(request => request.CertificateType)
                .Empty().WithMessage("Una credencial no lleva un motivo de certificado.")
                .When(request => !IsCertificate(request.Kind));

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

    public static IEndpointRouteBuilder MapRequestDocumentRequest(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/document-requests", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RequestDocumentRequest))
            .WithSummary("Asks for a batch of documents to be printed by SportFrog.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
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

        // Named here so a request for a design that does not exist, or is
        // not a certificate's, is refused the moment it is raised rather
        // than discovered when the platform organization tries to approve
        // it. Re-resolved again at approval regardless — a design can be
        // retired in between, and approval has to trust nothing it was not
        // just told again.
        if (kind == DocumentKind.Certificate
            && (await RequestDocumentBatch.ResolveTemplateAsync(
                database, request.TemplateId!.Value, kind, cancellationToken)).Refusal is { } refusal)
        {
            return refusal;
        }

        var documentRequest = new DocumentRequest
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            Kind = kind,
            TemplateId = request.TemplateId,
            CertificateType = request.CertificateType?.Trim(),
            CompetitionId = request.CompetitionId,
            CategoryId = request.CategoryId,
            TeamId = request.TeamId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RequestedBy = organization.UserId ?? Guid.Empty,
        };

        database.DocumentRequests.Add(documentRequest);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/document-requests/{documentRequest.Id}",
            new Response(documentRequest.Id, documentRequest.Status));
    }

    private static IResult Refuse(string property, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [property] = [message],
        });
}
