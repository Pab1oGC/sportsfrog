using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Teams;

/// <summary>
/// Whether a team has a history that removing it would orphan.
/// </summary>
/// <remarks>
/// Matches reference teams with ON DELETE RESTRICT, but that protection never
/// fires here: removing a team is a logical delete, so nothing is deleted as
/// far as the database is concerned and the foreign key is satisfied by a row
/// that has merely become invisible. A fixture would go on naming a team
/// nobody can read.
///
/// So the check is the guard, not a nicer message in front of one. Both
/// directions are counted, because a team appears in a fixture as either
/// side.
///
/// Written as SQL because matches have no entity yet; the table is already in
/// the schema. When the results module lands this becomes one query and
/// nothing outside this file changes.
/// </remarks>
internal sealed class TeamUsage(SportFrogDbContext database)
{
    public async Task<bool> HasMatchesAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var fixtures = await database.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM matches
                 WHERE deleted_at IS NULL
                   AND (home_team_id = {teamId} OR away_team_id = {teamId})
                 """)
            .SingleAsync(cancellationToken);

        return fixtures > 0;
    }
}
