using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Adds a division to a competition.
/// </summary>
/// <remarks>
/// Allowed at any point in the competition's life, unlike editing one. A
/// category that has just been created has no teams and no fixtures, so
/// nothing that was already played can be affected by its existence — an
/// organizer who opens a new age group in week three is adding to the event,
/// not restating it.
/// </remarks>
public static class CreateCategory
{
    public sealed record Response(Guid Id);

    public static IEndpointRouteBuilder MapCreateCategory(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/categories", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(CreateCategory))
            .WithSummary("Adds a category to a competition.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        CategoryContract contract,
        SportFrogDbContext database,
        CategoryPolicy policy,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        if (await policy.InspectRulesetOverrideAsync(
                competition, contract.RulesetId, cancellationToken) is { } violation)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [violation.Property] = [violation.Message],
            });
        }

        var name = contract.Name.Trim();

        if (await database.Categories.AnyAsync(
                category => category.CompetitionId == competitionId && category.Name == name,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Esta competencia ya tiene una categoría con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competitionId,
            RulesetId = contract.RulesetId,
            Name = name,
            Gender = Sex.Normalize(contract.Gender),
            BirthDateFrom = contract.BirthDateFrom,
            BirthDateTo = contract.BirthDateTo,
            MaxRosterSize = contract.MaxRosterSize,
            DisplayOrder = contract.DisplayOrder,
            QualifiersPerGroup = contract.QualifiersPerGroup,
        };

        database.Categories.Add(category);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "Esta competencia ya tiene una categoría con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Created(
            $"/competitions/{competitionId}/categories/{category.Id}",
            new Response(category.Id));
    }
}
