using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Whether anything is already being played under a ruleset.
///
/// The question matters twice. A ruleset in use cannot be removed, and its
/// configuration cannot be rewritten: both would reach backwards into results
/// that were recorded, ranked and published under rules that said something
/// else.
/// </summary>
/// <remarks>
/// This was raw SQL while competitions and categories had no entities. They
/// have them now, so it is an ordinary query, and nothing outside this file
/// had to change for that — which is what putting the question in one place
/// was for.
///
/// A category counts as a use only when it overrides the ruleset itself.
/// Categories that follow their competition are reached through the
/// competition, and counting them again would report one use as two.
///
/// The visibility filters are set aside deliberately. A withdrawn competition
/// is hidden from the people using the product but its row is still there,
/// still holding the foreign key; asking only about visible rows would answer
/// that the ruleset is free and then have the database refuse the delete. The
/// question here is whether anything references it, not whether anything
/// anyone can see does.
///
/// The count runs under the isolation policies like every other read, so it
/// only ever sees this organization. That is correct for the message it
/// produces, and it is not what the rule rests on: the foreign keys refuse
/// the delete whether the reference is visible from here or not.
/// </remarks>
internal sealed class RulesetUsage(SportFrogDbContext database)
{
    public async Task<bool> IsInUseAsync(Guid rulesetId, CancellationToken cancellationToken) =>
        await database.Competitions
            .IgnoreQueryFilters()
            .AnyAsync(competition => competition.RulesetId == rulesetId, cancellationToken)
        || await database.Categories
            .IgnoreQueryFilters()
            .AnyAsync(category => category.RulesetId == rulesetId, cancellationToken);
}
