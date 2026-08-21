using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Categories;

/// <summary>Something a category says that its competition does not allow.</summary>
internal sealed record CategoryViolation(string Property, string Message);

/// <summary>
/// The checks a category cannot make on its own, because they are about the
/// competition it belongs to.
/// </summary>
/// <remarks>
/// Shared between creating and editing for the same reason the ruleset module
/// shares its own: both ask the identical question, and two copies of the
/// answer drift.
/// </remarks>
internal sealed class CategoryPolicy(SportFrogDbContext database)
{
    /// <summary>
    /// Whether an overriding ruleset may be used here.
    /// </summary>
    /// <remarks>
    /// Two things have to hold, and the schema enforces neither. The ruleset
    /// has to exist and belong to this organization, which the isolation
    /// policy answers by simply not returning anyone else's; and it has to be
    /// written for the sport being played, because a volleyball ruleset
    /// applied to a football category prices set scores that the matches can
    /// never produce.
    /// </remarks>
    public async Task<CategoryViolation?> InspectRulesetOverrideAsync(
        Competition competition,
        Guid? rulesetId,
        CancellationToken cancellationToken)
    {
        if (rulesetId is not { } id)
        {
            // No override. The competition's own ruleset applies, and it was
            // already checked against the sport when the competition was set
            // up.
            return null;
        }

        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (ruleset is null)
        {
            return new CategoryViolation(
                "RulesetId",
                "No ruleset of this organization has that identifier.");
        }

        if (ruleset.SportCode != competition.SportCode)
        {
            return new CategoryViolation(
                "RulesetId",
                $"That ruleset is written for {ruleset.SportCode}, and this competition plays " +
                $"{competition.SportCode}. A category can vary the rules of its competition, " +
                "not the sport.");
        }

        return null;
    }
}
