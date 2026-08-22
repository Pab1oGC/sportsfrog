using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>Corrects the data of a registered person.</summary>
public static class UpdateAthlete
{
    public sealed record Request(
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        string? Gender,
        string? GuardianName,
        string? GuardianPhone,
        string? PhotoUrl,
        bool IsActive);

    /// <summary>
    /// What an empty photograph means: remove the one on file.
    /// </summary>
    /// <remarks>
    /// The field has three states rather than two, and it has to. Since
    /// photographs moved into object storage a reader receives a temporary
    /// link, not the image, so a client editing a person's telephone number
    /// has nothing to send back that would mean "the same photograph as
    /// before" — and a correction that silently deleted the picture every
    /// time somebody fixed a surname would be a very quiet way to lose them
    /// all.
    ///
    /// So an absent photograph leaves the existing one alone, an empty one
    /// removes it, and a data URL replaces it.
    /// </remarks>
    private const string RemovePhoto = "";

    private static readonly DateOnly EarliestPlausibleBirth = new(1900, 1, 1);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.FirstName)
                .NotEmpty().WithMessage("The first name is required.")
                .MaximumLength(80);

            RuleFor(request => request.LastName)
                .NotEmpty().WithMessage("The last name is required.")
                .MaximumLength(80);

            RuleFor(request => request.DocumentId)
                .NotEmpty().WithMessage("The identity document is required.")
                .MaximumLength(40);

            RuleFor(request => request.BirthDate)
                .Must(date => date > EarliestPlausibleBirth
                    && date < DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("The date of birth is not a plausible date.");

            // Checked because a category admits one of these and a roster is
            // accepted by comparing the two. Free text here would let an
            // athlete be recorded in a form no category can ever match, and
            // the mismatch would surface as an eligible player being refused
            // rather than as a refusal to save this.
            RuleFor(request => request.Gender)
                .Must(gender => Sex.IsAcceptable(gender))
                .When(request => request.Gender is not null)
                .WithMessage(Sex.Requirement);

            RuleFor(request => request.PhotoUrl)
                .Must(InlinePhoto.IsAcceptable)
                .When(request => request.PhotoUrl != RemovePhoto)
                .WithMessage(InlinePhoto.Requirement);
        }
    }

    public static IEndpointRouteBuilder MapUpdateAthlete(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/athletes/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(UpdateAthlete))
            .WithSummary("Corrects the data of a registered person.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        AthletePhoto photos,
        CancellationToken cancellationToken)
    {
        var athlete = await database.Athletes.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (athlete is null)
        {
            return Results.NotFound();
        }

        var document = request.DocumentId.Trim();

        // Correcting a mistyped document is legitimate; typing one that
        // already names somebody else is not, and would merge two people's
        // histories into one.
        if (await database.Athletes.AnyAsync(
                other => other.Id != id && other.DocumentId == document, cancellationToken))
        {
            return Results.Problem(
                detail: "Another registered person already has that identity document.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var replaced = athlete.PhotoKey;

        switch (request.PhotoUrl)
        {
            case null:
                break;

            case RemovePhoto:
                athlete.PhotoKey = null;
                break;

            default:
                if (await photos.StoreAsync(id, request.PhotoUrl, cancellationToken)
                    is not { } stored)
                {
                    return AthletePhoto.NotAnImage();
                }

                athlete.PhotoKey = stored;
                break;
        }

        athlete.FirstName = request.FirstName.Trim();
        athlete.LastName = request.LastName.Trim();
        athlete.DocumentId = document;
        athlete.BirthDate = request.BirthDate;
        athlete.Gender = Sex.Normalize(request.Gender);
        athlete.GuardianName = request.GuardianName?.Trim();
        athlete.GuardianPhone = request.GuardianPhone?.Trim();
        athlete.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        // Only once the row has been pointed somewhere else, and only if it
        // actually moved. Dropping the old image before the save would leave
        // an athlete with no photograph if the save then failed.
        if (replaced is not null && replaced != athlete.PhotoKey)
        {
            await photos.ForgetAsync(replaced, cancellationToken);
        }

        return Results.NoContent();
    }
}
