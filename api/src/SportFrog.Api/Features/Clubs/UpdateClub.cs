using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Clubs;

/// <summary>Edits a club of the active organization.</summary>
public static class UpdateClub
{
    /// <summary>
    /// What an empty crest means: remove the one on file. Same three states as
    /// <see cref="Athletes.UpdateAthlete"/>'s photograph, for the same reason:
    /// a reader gets a temporary link back, never the picture, so there is
    /// nothing it could send to mean "leave it as it is" other than absence.
    /// </summary>
    private const string RemoveLogo = "";

    public sealed record Request(string Name, string? ShortName, string? LogoUrl, string? ContactEmail, bool IsActive);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("El nombre del club es obligatorio.")
                .MaximumLength(120);

            RuleFor(request => request.ShortName)
                .MaximumLength(20)
                .When(request => request.ShortName is not null)
                .WithMessage("La abreviatura tiene como máximo 20 caracteres.");

            RuleFor(request => request.LogoUrl)
                .Must(InlinePhoto.IsAcceptable)
                .When(request => request.LogoUrl != RemoveLogo)
                .WithMessage(InlinePhoto.Requirement);

            RuleFor(request => request.ContactEmail)
                .Must(email => Email.TryParse(email, out _))
                .When(request => !string.IsNullOrWhiteSpace(request.ContactEmail))
                .WithMessage("El correo de contacto no tiene un formato válido.");
        }
    }

    public static IEndpointRouteBuilder MapUpdateClub(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/clubs/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(UpdateClub))
            .WithSummary("Edits a club.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        ClubPhoto photos,
        CancellationToken cancellationToken)
    {
        var club = await database.Clubs.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (club is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();

        if (await database.Clubs.AnyAsync(
                other => other.Id != id && other.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "Ya existe un club con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var replaced = club.LogoUrl;

        switch (request.LogoUrl)
        {
            case null:
                break;

            case RemoveLogo:
                club.LogoUrl = null;
                break;

            default:
                if (await photos.StoreAsync(id, request.LogoUrl, cancellationToken)
                    is not { } stored)
                {
                    return ClubPhoto.NotAnImage();
                }

                club.LogoUrl = stored;
                break;
        }

        club.Name = name;
        club.ShortName = request.ShortName?.Trim();
        club.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();

        // Deactivating is not deleting: an inactive club keeps its history and
        // stops being offered for new competitions.
        club.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        // Only once the row has been pointed somewhere else, and only if it
        // actually moved. Dropping the old crest before the save would leave
        // a club with no logo if the save then failed.
        if (replaced is not null && replaced != club.LogoUrl)
        {
            await photos.ForgetAsync(replaced, cancellationToken);
        }

        return Results.NoContent();
    }
}
