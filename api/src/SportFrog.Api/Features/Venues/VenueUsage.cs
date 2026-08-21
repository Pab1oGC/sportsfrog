using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Venues;

/// <summary>
/// Whether anything points at a place, by any route.
/// </summary>
/// <remarks>
/// This is the guard, not a friendlier message in front of one, and the
/// references involved are the reason.
///
/// A match points at a space with ON DELETE SET NULL, so removing a space
/// does not fail — it quietly empties the venue out of every fixture that was
/// placed on it. A calendar that loses where it is being played, without an
/// error anywhere, is the worst shape a data loss can take: nobody finds out
/// until somebody drives to the wrong ground.
///
/// A space points at its venue with ON DELETE CASCADE, so removing a venue
/// reaches the same outcome one step further along. That is why the venue
/// question is asked across all of its spaces rather than about the venue
/// itself.
///
/// And a competition names spaces inside its settings document, which no
/// foreign key covers at all — the schema documents
/// <c>settings.schedule.spaces[].venue_space_id</c>, and jsonb has no
/// referential integrity of any kind. Nothing writes that section today, so
/// this half currently finds nothing; it is here because the check belongs
/// with the question rather than with the module that will start filling it
/// in.
///
/// Deleted fixtures are counted too. Their rows still hold the reference, so
/// they would still be emptied, and a match restored afterwards would come
/// back without a place to be played.
///
/// Written as SQL because matches have no entity yet, and because the jsonb
/// half would be SQL regardless.
/// </remarks>
internal sealed class VenueUsage(SportFrogDbContext database)
{
    public Task<bool> IsSpaceInUseAsync(Guid spaceId, CancellationToken cancellationToken) =>
        AnyAsync(
            $"""
             SELECT (
                 EXISTS (
                     SELECT 1 FROM matches
                     WHERE venue_space_id = {spaceId}
                 )
                 OR EXISTS (
                     SELECT 1
                     FROM competitions c
                     CROSS JOIN LATERAL jsonb_array_elements(
                         CASE
                             WHEN jsonb_typeof(c.settings -> 'schedule' -> 'spaces') = 'array'
                             THEN c.settings -> 'schedule' -> 'spaces'
                             ELSE '[]'::jsonb
                         END) AS slot
                     WHERE lower(slot ->> 'venue_space_id') = lower({spaceId}::text)
                 )
             )::int AS "Value"
             """,
            cancellationToken);

    public Task<bool> IsVenueInUseAsync(Guid venueId, CancellationToken cancellationToken) =>
        AnyAsync(
            $"""
             SELECT (
                 EXISTS (
                     SELECT 1
                     FROM matches m
                     JOIN venue_spaces s ON s.id = m.venue_space_id
                     WHERE s.venue_id = {venueId}
                 )
                 OR EXISTS (
                     SELECT 1
                     FROM competitions c
                     CROSS JOIN LATERAL jsonb_array_elements(
                         CASE
                             WHEN jsonb_typeof(c.settings -> 'schedule' -> 'spaces') = 'array'
                             THEN c.settings -> 'schedule' -> 'spaces'
                             ELSE '[]'::jsonb
                         END) AS slot
                     WHERE lower(slot ->> 'venue_space_id') IN (
                         SELECT lower(id::text) FROM venue_spaces WHERE venue_id = {venueId}
                     )
                 )
             )::int AS "Value"
             """,
            cancellationToken);

    /// <summary>
    /// Runs a query shaped to return one boolean as an integer.
    /// </summary>
    /// <remarks>
    /// The two conditions are combined in SQL rather than as two round trips
    /// so that neither can be forgotten at a call site, and so the second is
    /// never paid for when the first already answered.
    /// </remarks>
    private async Task<bool> AnyAsync(
        FormattableString query,
        CancellationToken cancellationToken) =>
        await database.Database.SqlQuery<int>(query).SingleAsync(cancellationToken) > 0;
}
