using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

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
        bool IsActive);

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

        athlete.FirstName = request.FirstName.Trim();
        athlete.LastName = request.LastName.Trim();
        athlete.DocumentId = document;
        athlete.BirthDate = request.BirthDate;
        athlete.Gender = request.Gender?.Trim();
        athlete.GuardianName = request.GuardianName?.Trim();
        athlete.GuardianPhone = request.GuardianPhone?.Trim();
        athlete.IsActive = request.IsActive;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
