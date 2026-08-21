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
    public sealed record Response(Guid Id);

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
        RulesetContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var name = contract.Name.Trim();

        if (await database.Rulesets.AnyAsync(ruleset => ruleset.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "A ruleset with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var ruleset = new Ruleset
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            SportCode = contract.SportCode,
            Name = name,
            Config = contract.Config,
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
