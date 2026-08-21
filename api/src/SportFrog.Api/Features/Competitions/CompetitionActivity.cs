using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// What has actually happened on the field, which is what several transitions
/// depend on.
/// </summary>
/// <remarks>
/// The state of a competition is a claim about its matches, and the two can
/// disagree: a competition can be marked as still being set up while a
/// referee is recording a result, if nothing checks. These are the checks.
///
/// Written as SQL because matches have no entity yet; the table is already in
/// the schema. Kept in one class so that when the results module lands, this
/// becomes two ordinary queries and no endpoint changes.
/// </remarks>
internal sealed class CompetitionActivity(SportFrogDbContext database)
{
    /// <summary>
    /// Whether any match has gone beyond being scheduled.
    /// </summary>
    /// <remarks>
    /// Postponed and cancelled fixtures do not count: neither produced a
    /// result, so redrawing the calendar around them loses nothing. A
    /// walkover does count — nobody played it, but it was awarded, and it
    /// stands in the table exactly like a match that was.
    /// </remarks>
    public Task<bool> HasResultsAsync(Guid competitionId, CancellationToken cancellationToken) =>
        AnyMatchAsync(
            competitionId,
            ["in_progress", "finished", "walkover"],
            cancellationToken);

    /// <summary>
    /// Whether a match is on the field right now.
    /// </summary>
    public Task<bool> HasMatchesUnderWayAsync(
        Guid competitionId,
        CancellationToken cancellationToken) =>
        AnyMatchAsync(competitionId, ["in_progress"], cancellationToken);

    private async Task<bool> AnyMatchAsync(
        Guid competitionId,
        string[] states,
        CancellationToken cancellationToken)
    {
        // The states are compared as text rather than cast to match_state,
        // so this query does not depend on the enum labels being registered
        // with the driver for a type it never materializes.
        var count = await database.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM matches
                 WHERE competition_id = {competitionId}
                   AND status::text = ANY({states})
                 """)
            .SingleAsync(cancellationToken);

        return count > 0;
    }
}
