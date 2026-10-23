using SportFrog.Api.Infrastructure.Storage;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>Saves a certificate design, for an organization named by the request.</summary>
/// <remarks>
/// A credential has none to save: its structure is fixed by decree and its
/// only choices — background, accent colour, legal notice — live in a
/// credential design, not a template. The validator below is what actually
/// closes that door; see its own comment.
///
/// Platform-only, and never the organization's own — see
/// <see cref="RequestDocumentBatch"/>'s own remarks on why a design is no
/// longer something an organization is trusted to produce for itself. This
/// is the one write <see cref="DocumentTemplate.IsCatalog"/> is allowed to
/// turn on, and only for the platform organization's own row; every other
/// organization gets a bespoke design of its own instead, run inside its own
/// isolation context the same way <c>ApproveDocumentRequest</c> runs
/// inside the asking organization's.
/// </remarks>
public static class CreateTemplate
{
    /// <param name="OrganizationId">
    /// Which organization the design is for — the platform's own, for a
    /// catalog row every organization may name, or one specific
    /// organization's, for a design only it sees.
    /// </param>
    /// <param name="Kind">
    /// Arrives as text and is parsed here, like every other enum on this API:
    /// a value nobody recognises should come back as a refusal naming the
    /// options, not as a failure to read the request at all.
    /// </param>
    /// <param name="IsCatalog">
    /// Whether every organization may name this design, not only
    /// <paramref name="OrganizationId"/>. Refused unless that is the platform
    /// organization's own id — see <c>ck_document_templates_catalog_is_certificate</c>'s
    /// own migration for the schema half of this rule.
    /// </param>
    public sealed record Request(
        Guid OrganizationId,
        string Kind,
        string Name,
        string PageSize,
        JsonElement Layout,
        bool IsDefault = false,
        bool IsCatalog = false);

    public sealed record Response(Guid Id, int Version);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.OrganizationId)
                .NotEmpty().WithMessage("La organización es obligatoria.");

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            // Certificates only. A credential's structure is fixed by decree and
            // its values live in a credential design, so a template of that kind
            // would be a design nothing reads.
            RuleFor(request => request.Kind)
                .Must(kind => WireEnum.TryParse<DocumentKind>(kind, out var parsed) && parsed == DocumentKind.Certificate)
                .WithMessage("Las plantillas son solo de certificados. Las credenciales se configuran en Diseños de credencial.");

            RuleFor(request => request.PageSize)
                .Must(TemplateDesign.HasPageSize)
                .WithMessage(PageSizes);

            // The layout is not checked here. It is raw JSON at this point and
            // reading it is where most of its refusals come from, so it is
            // read and inspected in the feature — where a failure can name the
            // property rather than the request.
        }
    }

    internal static string PageSizes =>
        "Tamaño desconocido. Disponibles: "
        + string.Join(", ", TemplateDesign.PageSizes.Select(size => size.Code))
        + ".";

    public static IEndpointRouteBuilder MapCreateTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/documents/templates", HandleAsync)
            .WithoutOrganizationContext()
            .WithName(nameof(CreateTemplate))
            .WithSummary("Saves a certificate design for the organization the request names.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        HttpContext context,
        SportFrogDbContext database,
        OrganizationJobScope scopes,
        CancellationToken cancellationToken)
    {
        if (!await PlatformAuthority.ReachesAsync(
                context.User, database, MembershipRole.Operator, cancellationToken))
        {
            return Results.Problem(
                detail: "Esta operación no está permitida.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!await database.Organizations.AnyAsync(
                candidate => candidate.Id == request.OrganizationId, cancellationToken))
        {
            return TemplateFaults.Refuse(
                "organizationId", "No hay ninguna organización con ese identificador.");
        }

        if (request.IsCatalog
            && request.OrganizationId != await PlatformAuthority.OrganizationIdAsync(database, cancellationToken))
        {
            return TemplateFaults.Refuse(
                "isCatalog", "Solo un diseño de la organización de la plataforma puede ser del catálogo.");
        }

        var kind = WireEnum.Parse<DocumentKind>(request.Kind);

        if (!LayoutReader.TryRead(request.Layout, out var layout, out var unreadable))
        {
            return TemplateFaults.Refuse([unreadable]);
        }

        // Everything wrong with the design at once. Somebody who has just
        // laid out a card should not learn about its problems one refusal at
        // a time.
        if (LayoutPolicy.Inspect(layout, kind, key => StorageKeys.Belongs(request.OrganizationId, key))
            is { Count: > 0 } faults)
        {
            return TemplateFaults.Refuse(faults);
        }

        var callerId = JwtAccessTokenIssuer.CurrentUserId(context.User);

        var created = await scopes.RunAsync(request.OrganizationId, callerId, async (services, tenantDb) =>
        {
            var writer = services.GetRequiredService<TemplateWriter>();

            var template = new DocumentTemplate
            {
                Id = Guid.NewGuid(),
                OrgId = request.OrganizationId,
                Kind = kind,
                Name = request.Name.Trim(),
                PageSize = request.PageSize,
                Layout = layout,
                IsDefault = request.IsDefault,
                IsCatalog = request.IsCatalog,
                Version = 1,
            };

            tenantDb.Set<DocumentTemplate>().Add(template);

            await writer.ClearOtherDefaultsAsync(template, cancellationToken);
            writer.Record(template);

            await tenantDb.SaveChangesAsync(cancellationToken);

            return new Response(template.Id, template.Version);
        }, cancellationToken);

        return Results.Created($"/documents/templates/{created.Id}", created);
    }
}

/// <summary>How a bad design is reported.</summary>
/// <remarks>
/// Shared between creating and editing so the two answer the same way: a
/// layout the editor could save and then not re-save would be maddening.
/// </remarks>
internal static class TemplateFaults
{
    public static IResult Refuse(IReadOnlyList<LayoutFault> faults) =>
        Results.ValidationProblem(faults
            .GroupBy(fault => fault.Property)
            .ToDictionary(
                group => group.Key,
                group => group.Select(fault => fault.Message).Distinct().ToArray()));

    /// <summary>One refusal about the request itself, not about the layout.</summary>
    public static IResult Refuse(string property, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [property] = [message] });
}
