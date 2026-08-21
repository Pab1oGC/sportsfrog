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
/// Written as SQL rather than as a query over entities because the entities
/// do not exist yet — competitions and categories are the next module, and
/// their tables are already in the schema with the foreign keys pointing
/// here. Isolating that in one method is the point of this class: when those
/// entities land, this becomes an ordinary navigation and nothing outside
/// this file has to change.
///
/// The count runs under the isolation policies like every other read, so it
/// only ever sees this organization. That is correct for the message it
/// produces, and it is not what the rule rests on: the foreign keys refuse
/// the delete whether the reference is visible from here or not.
/// </remarks>
internal sealed class RulesetUsage(SportFrogDbContext database)
{
    public async Task<bool> IsInUseAsync(Guid rulesetId, CancellationToken cancellationToken)
    {
        // Both tables reference rulesets: a competition binds one for the
        // whole event, a category may override it for its own draw.
        var references = await database.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM (
                     SELECT 1 FROM competitions WHERE ruleset_id = {rulesetId}
                     UNION ALL
                     SELECT 1 FROM categories   WHERE ruleset_id = {rulesetId}
                 ) AS uses
                 """)
            .SingleAsync(cancellationToken);

        return references > 0;
    }
}
