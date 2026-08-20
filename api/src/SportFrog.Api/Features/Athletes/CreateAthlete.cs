using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Athletes;

/// <summary>Registers a person in the active organization (RF-08).</summary>
public static class CreateAthlete
{
    public sealed record Request(
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        string? Gender,
        string? GuardianName,
        string? GuardianPhone);

    /// <param name="AlreadyRegistered">
    /// True when the document already named someone here. The existing person
    /// is returned instead of a second one being created.
    /// </param>
    public sealed record Response(Guid Id, bool AlreadyRegistered);

    /// <summary>Nobody in these competitions was born before this.</summary>
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

            // The anchor of the person's identity within the organization
            // (RF-43), so a blank one would let two people become one.
            RuleFor(request => request.DocumentId)
                .NotEmpty().WithMessage("The identity document is required.")
                .MaximumLength(40);

            RuleFor(request => request.BirthDate)
                .Must(date => date > EarliestPlausibleBirth
                    && date < DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("The date of birth is not a plausible date.");
        }
    }

    public static IEndpointRouteBuilder MapCreateAthlete(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/athletes", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(CreateAthlete))
            .WithSummary("Registers a person.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var document = request.DocumentId.Trim();

        // Registering someone who is already here is not an error: it is the
        // ordinary case between seasons. Returning the existing person is what
        // keeps their accumulated history attached to them (RF-43, RF-27)
        // instead of starting a second, emptier record.
        var existing = await database.Athletes.SingleOrDefaultAsync(
            candidate => candidate.DocumentId == document, cancellationToken);

        if (existing is not null)
        {
            return Results.Ok(new Response(existing.Id, AlreadyRegistered: true));
        }

        var athlete = new Athlete
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DocumentId = document,
            BirthDate = request.BirthDate,
            Gender = request.Gender?.Trim(),
            GuardianName = request.GuardianName?.Trim(),
            GuardianPhone = request.GuardianPhone?.Trim(),
        };

        database.Athletes.Add(athlete);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/athletes/{athlete.Id}",
            new Response(athlete.Id, AlreadyRegistered: false));
    }
}
