using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>Registers a person in the active organization (RF-08).</summary>
public static class CreateAthlete
{
    /// <param name="WeightKg">
    /// The most recent weigh-in on record, if one is being entered now. What
    /// a weight-classed category's eligibility check reads — see
    /// <c>RosterPolicy</c>.
    /// </param>
    public sealed record Request(
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        string? Gender,
        string? GuardianName,
        string? GuardianPhone,
        string? PhotoUrl,
        decimal? WeightKg = null);

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
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .MaximumLength(80);

            RuleFor(request => request.LastName)
                .NotEmpty().WithMessage("El apellido es obligatorio.")
                .MaximumLength(80);

            // The anchor of the person's identity within the organization
            // (RF-43), so a blank one would let two people become one.
            RuleFor(request => request.DocumentId)
                .NotEmpty().WithMessage("El documento de identidad es obligatorio.")
                .MaximumLength(40);

            RuleFor(request => request.BirthDate)
                .Must(date => date > EarliestPlausibleBirth
                    && date < DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("La fecha de nacimiento no es una fecha posible.");

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
                .WithMessage(InlinePhoto.Requirement);

            // Mirrors ck_athletes_weight_positive: a weight of zero or less
            // is not a real weigh-in.
            RuleFor(request => request.WeightKg)
                .GreaterThan(0)
                .When(request => request.WeightKg is not null)
                .WithMessage("El peso tiene que ser mayor que cero.");
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
        AthletePhoto photos,
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

        // The identifier is settled here rather than by the database because
        // the photograph is filed under it, and the picture has to be stored
        // before the row that points at it exists.
        var athleteId = Guid.NewGuid();

        string? photoKey = null;

        if (request.PhotoUrl is { Length: > 0 } upload)
        {
            // After the duplicate check, so registering somebody who is
            // already on the register does not leave their photograph behind
            // in the bucket. Before the insert, so a row never points at
            // something that was never written — the reverse leaves a broken
            // reference, which is worse than a few unreferenced kilobytes.
            photoKey = await photos.StoreAsync(athleteId, upload, cancellationToken);

            if (photoKey is null)
            {
                return AthletePhoto.NotAnImage();
            }
        }

        var athlete = new Athlete
        {
            Id = athleteId,
            OrgId = organization.RequireOrganizationId(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DocumentId = document,
            BirthDate = request.BirthDate,
            Gender = Sex.Normalize(request.Gender),
            GuardianName = request.GuardianName?.Trim(),
            GuardianPhone = request.GuardianPhone?.Trim(),
            PhotoKey = photoKey,
            WeightKg = request.WeightKg,
        };

        database.Athletes.Add(athlete);
        await database.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/athletes/{athlete.Id}",
            new Response(athlete.Id, AlreadyRegistered: false));
    }
}
