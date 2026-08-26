using SportFrog.Api.Infrastructure.Storage;
using System.Text.Json;
using FluentValidation;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>Saves a design for a credential or a certificate.</summary>
public static class CreateTemplate
{
    /// <param name="Kind">
    /// Arrives as text and is parsed here, like every other enum on this API:
    /// a value nobody recognises should come back as a refusal naming the
    /// options, not as a failure to read the request at all.
    /// </param>
    public sealed record Request(
        string Kind,
        string Name,
        string PageSize,
        JsonElement Layout,
        bool IsDefault = false);

    public sealed record Response(Guid Id, int Version);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            RuleFor(request => request.Kind)
                .Must(kind => WireEnum.TryParse<DocumentKind>(kind, out _))
                .WithMessage($"Tipo desconocido. Disponibles: {WireEnum.Options<DocumentKind>()}.");

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
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CreateTemplate))
            .WithSummary("Saves a design for a credential or a certificate.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        TemplateWriter writer,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var kind = WireEnum.Parse<DocumentKind>(request.Kind);
        var organizationId = organization.RequireOrganizationId();

        if (!LayoutReader.TryRead(request.Layout, out var layout, out var unreadable))
        {
            return TemplateFaults.Refuse([unreadable]);
        }

        // Everything wrong with the design at once. Somebody who has just
        // laid out a card should not learn about its problems one refusal at
        // a time.
        if (LayoutPolicy.Inspect(layout, kind, key => StorageKeys.Belongs(organizationId, key)) is { Count: > 0 } faults)
        {
            return TemplateFaults.Refuse(faults);
        }

        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            OrgId = organizationId,
            Kind = kind,
            Name = request.Name.Trim(),
            PageSize = request.PageSize,
            Layout = layout,
            IsDefault = request.IsDefault,
            Version = 1,
        };

        database.Set<DocumentTemplate>().Add(template);

        await writer.ClearOtherDefaultsAsync(template, cancellationToken);
        writer.Record(template);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/documents/templates/{template.Id}", new Response(template.Id, template.Version));
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
}
