using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Corrects a category of a competition.
/// </summary>
/// <remarks>
/// Eligibility stops being editable once the competition leaves draft, and
/// the reason is that rosters were accepted against it. Narrowing the birth
/// window in week three does not remove the players already registered
/// outside it; it leaves a category whose stated rule and whose actual squads
/// disagree, and nothing in the system would ever report that.
///
/// The same goes for the ruleset override, which decides how the category's
/// own table is scored, and for the roster cap, which teams have already been
/// filled up to.
///
/// The name and the order they are listed in are labels, and labels get
/// corrected.
/// </remarks>
public static class UpdateCategory
{
    public static IEndpointRouteBuilder MapUpdateCategory(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{competitionId:guid}/categories/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(UpdateCategory))
            .WithSummary("Corrects a category.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid id,
        CategoryContract contract,
        SportFrogDbContext database,
        CategoryPolicy policy,
        CancellationToken cancellationToken)
    {
        var category = await database.Categories.SingleOrDefaultAsync(
            candidate => candidate.CompetitionId == competitionId && candidate.Id == id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var competition = await database.Competitions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == competitionId, cancellationToken);

        var gender = Sex.Normalize(contract.Gender);

        if (competition.Status != CompetitionState.Draft && EligibilityChanged(category, contract, gender))
        {
            return Results.Problem(
                detail: "Esta competencia ya salió de borrador, así que a quién admite esta " +
                        "categoría quedó fijo. Las nóminas se aceptaron contra estas reglas, y " +
                        "cambiarlas ahora no quitaría a los jugadores ya registrados bajo ellas.",
                statusCode: StatusCodes.Status409Conflict);
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
                other => other.CompetitionId == competitionId
                    && other.Id != id
                    && other.Name == name,
                cancellationToken))
        {
            return Results.Problem(
                detail: "Esta competencia ya tiene una categoría con ese nombre.",
                statusCode: StatusCodes.Status409Conflict);
        }

        category.RulesetId = contract.RulesetId;
        category.Name = name;
        category.Gender = gender;
        category.BirthDateFrom = contract.BirthDateFrom;
        category.BirthDateTo = contract.BirthDateTo;
        category.MaxRosterSize = contract.MaxRosterSize;
        category.DisplayOrder = contract.DisplayOrder;
        category.QualifiersPerGroup = contract.QualifiersPerGroup;
        category.MinWeightKg = contract.MinWeightKg;
        category.MaxWeightKg = contract.MaxWeightKg;
        category.UsesRepechage = contract.UsesRepechage;

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

        return Results.NoContent();
    }

    /// <summary>
    /// Whether the request changes anything a roster was accepted against.
    /// </summary>
    /// <remarks>
    /// Compared against the stored values rather than asked of the change
    /// tracker, unlike the ruleset module: these are plain columns, so an
    /// equality check here says exactly what it means, and singling out which
    /// of the contract's fields actually decide eligibility is the whole
    /// point — only the name and the display order are labels, and everything
    /// else here was already asked of a roster the moment it registered.
    /// </remarks>
    private static bool EligibilityChanged(
        Category category,
        CategoryContract contract,
        string? gender) =>
        category.RulesetId != contract.RulesetId
        || category.Gender != gender
        || category.BirthDateFrom != contract.BirthDateFrom
        || category.BirthDateTo != contract.BirthDateTo
        || category.MaxRosterSize != contract.MaxRosterSize
        || category.MinWeightKg != contract.MinWeightKg
        || category.MaxWeightKg != contract.MaxWeightKg;
}
