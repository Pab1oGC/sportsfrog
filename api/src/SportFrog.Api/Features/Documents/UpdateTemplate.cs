using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Configurations;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Changes a design.
/// </summary>
/// <remarks>
/// The kind never changes. A credential redrawn as a certificate is not an
/// edit, it is a different document, and the cards already issued from this
/// template say what kind they were — changing it would rewrite what they
/// claim to be.
///
/// A changed layout gets a new version and the old one stays. A rename does
/// not: the number is about the design, and bumping it because somebody fixed
/// a typo in the name would fill the archive with copies of an identical
/// card and make "printed under version 4" mean nothing.
/// </remarks>
public static class UpdateTemplate
{
    public sealed record Request(
        string Name,
        string PageSize,
        JsonElement Layout,
        bool IsDefault = false);

    public sealed record Response(int Version, bool Redesigned);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            RuleFor(request => request.PageSize)
                .Must(TemplateDesign.HasPageSize)
                .WithMessage(CreateTemplate.PageSizes);

            // The layout is read in the feature, for the reason the create
            // path explains: a JSON reader failure never reaches a validator.
        }
    }

    public static IEndpointRouteBuilder MapUpdateTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/documents/templates/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(UpdateTemplate))
            .WithSummary("Changes a design, keeping what it looked like before.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        TemplateWriter writer,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var template = await database.Set<DocumentTemplate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (template is null)
        {
            return Results.NotFound();
        }

        var organizationId = organization.RequireOrganizationId();

        if (!LayoutReader.TryRead(request.Layout, out var layout, out var unreadable))
        {
            return TemplateFaults.Refuse([unreadable]);
        }

        if (LayoutPolicy.Inspect(layout, template.Kind, organizationId)
            is { Count: > 0 } faults)
        {
            return TemplateFaults.Refuse(faults);
        }

        // Whether the card itself changed, decided by comparing what would be
        // stored rather than the objects — two layouts built from the same
        // request are never the same instance, and every save would otherwise
        // look like a redesign.
        var redesigned = !LayoutStorage.Comparer.Equals(template.Layout, layout)
            || template.PageSize != request.PageSize;

        template.Name = request.Name.Trim();
        template.PageSize = request.PageSize;
        template.Layout = layout;
        template.IsDefault = request.IsDefault;

        if (redesigned)
        {
            template.Version++;
            writer.Record(template);
        }

        await writer.ClearOtherDefaultsAsync(template, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(template.Version, redesigned));
    }
}
