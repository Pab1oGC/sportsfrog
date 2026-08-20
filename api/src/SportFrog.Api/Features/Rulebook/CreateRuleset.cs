using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Writes down how an organization plays a sport.
/// </summary>
public static class CreateRuleset
{
    public sealed record Request(string SportCode, string Name, RulesetConfiguration Config);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator(RulesetPolicy policy)
        {
            RuleFor(request => request.SportCode)
                .NotEmpty().WithMessage("The sport is required.")
                .MaximumLength(50);

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The ruleset name is required.")
                .MaximumLength(120);

            RuleFor(request => request.Config)
                .NotNull().WithMessage("The configuration is required.")
                .SetValidator(new RulesetShapeValidator());

            // Held back until the structure is sound, so a configuration
            // missing its periods is reported as missing them rather than as
            // pricing the wrong outcomes for a number of sets it never gave.
            RuleFor(request => request)
                .CustomAsync(async (request, context, cancellationToken) =>
                {
                    var violations = await policy.InspectAsync(
                        request.SportCode, request.Config, cancellationToken);

                    foreach (var violation in violations)
                    {
                        context.AddFailure(
                            new ValidationFailure(violation.Property, violation.Message));
                    }
                })
                .When(request => request.SportCode is { Length: > 0 }
                    && request.Config is { Periods: not null, Points: not null, Tiebreakers: not null });
        }
    }

    public static IEndpointRouteBuilder MapCreateRuleset(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/rulesets", HandleAsync)
            // Higher than the club and athlete endpoints, which an operator
            // reaches. A ruleset decides how every competition bound to it is
            // scored and ranked, so editing one reaches back into results
            // already recorded under it. That is a decision for whoever
            // administers the organization, not for whoever runs a fixture.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(CreateRuleset))
            .WithSummary("Registers a ruleset.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await database.Rulesets.AnyAsync(
                ruleset => ruleset.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "A ruleset with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var ruleset = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            SportCode = request.SportCode,
            Name = name,
            Config = request.Config,
        };

        database.Rulesets.Add(ruleset);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests naming the same ruleset at once. The check above
            // settles the ordinary case and this settles the race, because
            // only the index sees both writes.
            return Results.Problem(
                detail: "A ruleset with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created($"/rulesets/{ruleset.Id}", new Response(ruleset.Id));
    }
}
