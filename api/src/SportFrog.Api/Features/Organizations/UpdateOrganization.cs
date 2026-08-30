using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Organizations;

/// <summary>
/// Edits the active organization's own profile: its display name and its
/// mark. Not its address — a slug is what every public link already points
/// at, and renaming it here would break every one of them the moment
/// somebody wanted a nicer display name.
/// </summary>
public static class UpdateOrganization
{
    /// <summary>
    /// What an empty mark means: remove the one on file. Same three states as
    /// a club's crest, for the same reason.
    /// </summary>
    private const string RemoveLogo = "";

    public sealed record Request(string Name, string? LogoUrl);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre de la organización es obligatorio.")
                .MaximumLength(120);

            RuleFor(request => request.LogoUrl)
                .Must(InlinePhoto.IsAcceptable)
                .When(request => request.LogoUrl != RemoveLogo)
                .WithMessage(InlinePhoto.Requirement);
        }
    }

    public static IEndpointRouteBuilder MapUpdateOrganization(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/organizations", HandleAsync)
            // Renaming the organization or changing its mark is
            // administration, not day-to-day operation of a competition.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(UpdateOrganization))
            .WithSummary("Edits the active organization's name and mark.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var org = await database.Organizations.SingleAsync(
            candidate => candidate.Id == organization.RequireOrganizationId(), cancellationToken);

        var replaced = org.LogoUrl;

        switch (request.LogoUrl)
        {
            case null:
                break;

            case RemoveLogo:
                org.LogoUrl = null;
                break;

            default:
                if (await pictures.StoreAsync("organizations", request.LogoUrl, cancellationToken)
                    is not { } stored)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["logoUrl"] = ["El logo no se pudo leer como una imagen."],
                    });
                }

                org.LogoUrl = stored;
                break;
        }

        org.Name = request.Name.Trim();

        await database.SaveChangesAsync(cancellationToken);

        if (replaced is not null && replaced != org.LogoUrl)
        {
            await pictures.ForgetAsync(replaced, cancellationToken);
        }

        return Results.NoContent();
    }
}
