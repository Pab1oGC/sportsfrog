using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// Which entity one route value names, for <see cref="ScopeResolver"/> to
/// resolve to the competition and/or club it belongs to.
/// </summary>
public enum ScopeTargetKind
{
    /// <summary>The route value already is a competition id.</summary>
    Competition,

    /// <summary>The route value already is a club id.</summary>
    Club,

    /// <summary>A category, resolved to the competition it belongs to.</summary>
    Category,

    /// <summary>
    /// A team, resolved to both the competition its category belongs to and
    /// the club that fields it — a team is the one target that genuinely
    /// belongs to both axes at once.
    /// </summary>
    Team,

    /// <summary>A match, resolved to the competition it belongs to.</summary>
    Match,

    /// <summary>A performance, resolved to the competition it belongs to.</summary>
    Performance,

    /// <summary>
    /// A player event, resolved to the competition of the match it was
    /// recorded in.
    /// </summary>
    PlayerEvent,

    /// <summary>
    /// A roster entry, resolved to both the competition its team's category
    /// belongs to and the club that team is fielded by — the same two-axis
    /// shape as <see cref="Team"/>, one step removed.
    /// </summary>
    RosterEntry,
}

/// <summary>
/// What one route value, of a given <see cref="ScopeTargetKind"/>, resolves
/// to. Either side is null when that target has no such owner — a category
/// belongs to a competition but no club, so its <see cref="ClubId"/> is
/// always null.
/// </summary>
public sealed record ResolvedScopeTarget(Guid? CompetitionId, Guid? ClubId);

/// <summary>
/// Turns a route value naming some business entity into the competition
/// and/or club it belongs to, for <c>RequireScopeExtensions</c> to compare
/// against what the caller's membership is scoped to.
/// </summary>
/// <remarks>
/// Deliberately a single class with one case per <see cref="ScopeTargetKind"/>
/// rather than a registry of pluggable strategies like
/// <c>IResultShapeRulesRegistry</c> or <c>ICalendarDrawRegistry</c>: those
/// exist because a new sport or draw format is a real, growing set of
/// business rules a third party might add to. What a route value resolves to
/// is fixed by the schema's own foreign keys — bounded, mechanical, and nobody
/// extends it without a migration anyway — so one method with one case per
/// kind (the same shape <c>ListXlsx.WriteCell</c> already uses for a closed,
/// schema-shaped set) says that plainly instead of implying an extension
/// point that isn't real.
/// </remarks>
public sealed class ScopeResolver(SportFrogDbContext database)
{
    /// <summary>
    /// Resolves one target, or <c>null</c> when no row of that kind has
    /// this id.
    /// </summary>
    /// <remarks>
    /// Not finding the row is deliberately not an error: the caller (an
    /// endpoint filter) must refuse exactly as it would for a real target
    /// outside the caller's scope, so a scoped membership probing a
    /// nonexistent id gets the same answer as one probing somebody else's —
    /// the existence of the row is never revealed by the authorization
    /// decision, only by whatever 404 the handler itself produces afterward.
    /// </remarks>
    public async Task<ResolvedScopeTarget?> ResolveAsync(
        ScopeTargetKind kind, Guid id, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case ScopeTargetKind.Competition:
                // The route value already is the answer: no query, and
                // never null — see RequireScopeExtensions for why that is
                // safe even when the id names nothing real.
                return new ResolvedScopeTarget(id, null);

            case ScopeTargetKind.Club:
                return new ResolvedScopeTarget(null, id);

            case ScopeTargetKind.Category:
            {
                var competitionId = await database.Categories
                    .Where(category => category.Id == id)
                    .Select(category => (Guid?)category.CompetitionId)
                    .SingleOrDefaultAsync(cancellationToken);

                return competitionId is { } found ? new ResolvedScopeTarget(found, null) : null;
            }

            case ScopeTargetKind.Team:
            {
                // No navigation property from Team to Category (it carries
                // only the scalar CategoryId), so the competition side needs
                // an explicit join — same shape ScheduleCalendar already
                // joins matches to categories with.
                var row = await (
                    from team in database.Teams
                    join category in database.Categories on team.CategoryId equals category.Id
                    where team.Id == id
                    select new { team.ClubId, category.CompetitionId }
                ).SingleOrDefaultAsync(cancellationToken);

                return row is null ? null : new ResolvedScopeTarget(row.CompetitionId, row.ClubId);
            }

            case ScopeTargetKind.Match:
            {
                var competitionId = await database.Matches
                    .Where(match => match.Id == id)
                    .Select(match => (Guid?)match.CompetitionId)
                    .SingleOrDefaultAsync(cancellationToken);

                return competitionId is { } found ? new ResolvedScopeTarget(found, null) : null;
            }

            case ScopeTargetKind.Performance:
            {
                var competitionId = await database.Performances
                    .Where(performance => performance.Id == id)
                    .Select(performance => (Guid?)performance.CompetitionId)
                    .SingleOrDefaultAsync(cancellationToken);

                return competitionId is { } found ? new ResolvedScopeTarget(found, null) : null;
            }

            case ScopeTargetKind.PlayerEvent:
            {
                // PlayerEvent carries only MatchId, not its own competition —
                // the same join shape as Team above, one level removed.
                var competitionId = await (
                    from playerEvent in database.PlayerEvents
                    join match in database.Matches on playerEvent.MatchId equals match.Id
                    where playerEvent.Id == id
                    select (Guid?)match.CompetitionId
                ).SingleOrDefaultAsync(cancellationToken);

                return competitionId is { } found ? new ResolvedScopeTarget(found, null) : null;
            }

            case ScopeTargetKind.RosterEntry:
            {
                // RosterEntry carries only TeamId, not its own club or
                // competition -- one join further than Team above.
                var row = await (
                    from entry in database.RosterEntries
                    join team in database.Teams on entry.TeamId equals team.Id
                    join category in database.Categories on team.CategoryId equals category.Id
                    where entry.Id == id
                    select new { team.ClubId, category.CompetitionId }
                ).SingleOrDefaultAsync(cancellationToken);

                return row is null ? null : new ResolvedScopeTarget(row.CompetitionId, row.ClubId);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scope target kind.");
        }
    }
}
