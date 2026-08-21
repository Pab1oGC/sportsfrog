using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Sets up a competition (RF-06).
/// </summary>
public static class CreateCompetition
{
    public sealed record Response(Guid Id, string SportCode);

    public static IEndpointRouteBuilder MapCreateCompetition(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions", HandleAsync)
            // An administrator's decision, like the ruleset it binds. Running
            // the competition afterwards — recording results, closing matches
            // — is the operator's work and is gated separately.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(CreateCompetition))
            .WithSummary("Sets up a competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        CompetitionContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        // Loaded rather than merely checked for existence: its sport is what
        // fills the competition's own sport column, so the row is needed
        // either way and asking a validator to confirm it first would be
        // paying for the same lookup twice.
        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == contract.RulesetId, cancellationToken);

        if (ruleset is null)
        {
            // Not found rather than forbidden, and not distinguishable from a
            // ruleset belonging to another organization: the isolation policy
            // never returned it, so there is nothing here that could tell
            // them apart.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["RulesetId"] = ["No ruleset of this organization has that identifier."],
            });
        }

        var slug = Slug.Normalize(contract.Slug);

        if (await database.Competitions.AnyAsync(
                competition => competition.Slug == slug, cancellationToken))
        {
            return Results.Problem(
                detail: "A competition already uses that address.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),

            // Taken from the ruleset, never from the request. The two cannot
            // disagree because only one of them was ever asked.
            SportCode = ruleset.SportCode,
            RulesetId = ruleset.Id,

            Name = contract.Name.Trim(),
            Slug = slug,
            Season = contract.Season.Trim(),
            Format = contract.Format,
            CaptureLevel = Enum.Parse<CaptureLevel>(contract.CaptureLevel, ignoreCase: true),

            // Every competition begins in draft. Nothing is published and
            // nothing is played until someone moves it on.
            Status = CompetitionState.Draft,

            StartsOn = contract.StartsOn,
            EndsOn = contract.EndsOn,
            IsPublic = false,
            Settings = contract.Settings ?? new CompetitionSettings(),
        };

        database.Competitions.Add(competition);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests claiming the same address at once. The check above
            // answers the ordinary case; only the index sees this one.
            return Results.Problem(
                detail: "A competition already uses that address.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/competitions/{competition.Id}",
            new Response(competition.Id, competition.SportCode));
    }
}
