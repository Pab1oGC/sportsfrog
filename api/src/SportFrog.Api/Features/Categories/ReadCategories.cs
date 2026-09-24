using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Lists the categories of a competition, or reads one.
/// </summary>
public static class ReadCategories
{
    public sealed record Summary(
        Guid Id,
        Guid CompetitionId,
        Guid? RulesetId,
        string? RulesetName,
        Guid EffectiveRulesetId,
        string Name,
        string? Gender,
        DateOnly? BirthDateFrom,
        DateOnly? BirthDateTo,
        short? MaxRosterSize,
        short DisplayOrder,
        short? QualifiersPerGroup,
        decimal? MinWeightKg,
        decimal? MaxWeightKg,
        bool UsesRepechage = false);

    public static IEndpointRouteBuilder MapReadCategories(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/categories", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCategories))
            .WithSummary("Lists the categories of a competition.");

        routes.MapGet("/competitions/{competitionId:guid}/categories/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadCategory")
            .WithSummary("Reads one category.");

        return routes;
    }

    /// <summary>
    /// The categories of one competition, in the order the organizers listed
    /// them.
    /// </summary>
    /// <remarks>
    /// The competition is checked first, and the extra query is the point: a
    /// competition that does not exist and one that exists with no categories
    /// are different answers, and both would come back as an empty list
    /// otherwise. A caller reading an empty array has no way to tell that the
    /// address it built was wrong.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid competitionId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.Competitions.AnyAsync(
                competition => competition.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.Categories
                .Where(category => category.CompetitionId == competitionId)
                .OrderBy(category => category.DisplayOrder)
                .ThenBy(category => category.Name))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid competitionId,
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        // Scoped to the competition in the route as well as by identifier, so
        // a category of a different competition is not found here rather than
        // returned under an address that does not describe it.
        var category = await Project(database.Categories
                .Where(candidate => candidate.CompetitionId == competitionId && candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return category is null ? Results.NotFound() : Results.Ok(category);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing
    /// the same category differently.
    /// </summary>
    /// <remarks>
    /// The effective ruleset is resolved here rather than left to the caller.
    /// Whoever reads a category is about to score a match under it, and the
    /// rule — the override if there is one, otherwise the competition's — is
    /// the same every time; answering it once here is safer than trusting
    /// every consumer to remember the fallback.
    ///
    /// The override is still reported separately, because "this category sets
    /// its own rules" and "this category follows the competition" are
    /// different facts about how it was configured.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<Category> categories) =>
        categories.Select(category => new Summary(
            category.Id,
            category.CompetitionId,
            category.RulesetId,
            category.Ruleset!.Name,
            category.RulesetId ?? category.Competition!.RulesetId,
            category.Name,
            category.Gender,
            category.BirthDateFrom,
            category.BirthDateTo,
            category.MaxRosterSize,
            category.DisplayOrder,
            category.QualifiersPerGroup,
            category.MinWeightKg,
            category.MaxWeightKg,
            category.UsesRepechage));
}
