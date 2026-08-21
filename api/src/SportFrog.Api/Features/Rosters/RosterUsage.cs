using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Whether anything was ever recorded against a registration.
/// </summary>
/// <remarks>
/// This is what separates the two ways of removing someone. A registration
/// with events behind it is not a mistake to be struck off — those events are
/// a goal that was scored and a card that was shown, and they belong to
/// somebody. The honest removal there is a withdrawal, which says the player
/// left and keeps what they did.
///
/// The database will not decide this for us. Player events reference the
/// entry with ON DELETE RESTRICT, but striking is a logical delete, so the
/// foreign key stays satisfied by a row that has merely become invisible —
/// and the statistics would go on counting goals for a player who, as far as
/// any query can tell, was never registered.
///
/// Written as SQL because player events have no entity yet; the table is
/// already in the schema. When the results module lands this becomes one
/// query and nothing outside this file changes.
/// </remarks>
internal sealed class RosterUsage(SportFrogDbContext database)
{
    public async Task<bool> HasRecordedEventsAsync(
        Guid rosterEntryId,
        CancellationToken cancellationToken)
    {
        var events = await database.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM player_events
                 WHERE roster_entry_id = {rosterEntryId}
                 """)
            .SingleAsync(cancellationToken);

        return events > 0;
    }
}
