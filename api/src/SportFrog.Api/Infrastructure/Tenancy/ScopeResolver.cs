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

    /// <summary>
    /// A staff row, resolved to both the competition its team's category
    /// belongs to and the club that team is fielded by — the same two-axis
    /// shape as <see cref="RosterEntry"/>, for the other kind of person a
    /// team has.
    /// </summary>
    TeamStaff,

    /// <summary>The route value already is a course id.</summary>
    Course,

    /// <summary>
    /// An enrollment, resolved to the course it belongs to — the same one
    /// step removed as <see cref="RosterEntry"/> is from <see cref="Team"/>,
    /// just on a single axis: a course has no club or competition of its
    /// own for an enrollment to also belong to.
    /// </summary>
    CourseEnrollment,

    /// <summary>
    /// A session, resolved to the course it belongs to — the same shape as
    /// <see cref="CourseEnrollment"/>.
    /// </summary>
    CourseSession,

    /// <summary>
    /// A pension charge, resolved to the course of the enrollment it was
    /// raised for — one join further than <see cref="CourseEnrollment"/>,
    /// the same shape <see cref="RosterEntry"/> is from <see cref="Team"/>.
    /// </summary>
    TuitionCharge,
}

/// <summary>
/// What one route value, of a given <see cref="ScopeTargetKind"/>, resolves
/// to. Each side is null when that target has no such owner — a category
/// belongs to a competition but no club, so its <see cref="ClubId"/> is
/// always null, and a course belongs to neither, so a course target's
/// <see cref="CompetitionId"/> and <see cref="ClubId"/> are both always
/// null.
/// </summary>
/// <param name="CourseId">
/// Defaults to null so every <c>ResolvedScopeTarget(competitionId, clubId)</c>
/// built before the course axis existed — this file's own and the test
/// suite's — keeps compiling unchanged.
/// </param>
public sealed record ResolvedScopeTarget(Guid? CompetitionId, Guid? ClubId, Guid? CourseId = null);

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

            case ScopeTargetKind.TeamStaff:
            {
                // The same two joins RosterEntry needs, for the same reason:
                // the row carries only TeamId.
                var row = await (
                    from staff in database.TeamStaff
                    join team in database.Teams on staff.TeamId equals team.Id
                    join category in database.Categories on team.CategoryId equals category.Id
                    where staff.Id == id
                    select new { team.ClubId, category.CompetitionId }
                ).SingleOrDefaultAsync(cancellationToken);

                return row is null ? null : new ResolvedScopeTarget(row.CompetitionId, row.ClubId);
            }

            case ScopeTargetKind.Course:
                // The route value already is the answer, same shape as
                // Competition and Club above.
                return new ResolvedScopeTarget(null, null, id);

            case ScopeTargetKind.CourseEnrollment:
            {
                var courseId = await database.CourseEnrollments
                    .Where(entry => entry.Id == id)
                    .Select(entry => (Guid?)entry.CourseId)
                    .SingleOrDefaultAsync(cancellationToken);

                return courseId is { } found ? new ResolvedScopeTarget(null, null, found) : null;
            }

            case ScopeTargetKind.CourseSession:
            {
                var courseId = await database.CourseSessions
                    .Where(session => session.Id == id)
                    .Select(session => (Guid?)session.CourseId)
                    .SingleOrDefaultAsync(cancellationToken);

                return courseId is { } found ? new ResolvedScopeTarget(null, null, found) : null;
            }

            case ScopeTargetKind.TuitionCharge:
            {
                var courseId = await (
                    from charge in database.TuitionCharges
                    join entry in database.CourseEnrollments on charge.EnrollmentId equals entry.Id
                    where charge.Id == id
                    select (Guid?)entry.CourseId
                ).SingleOrDefaultAsync(cancellationToken);

                return courseId is { } found ? new ResolvedScopeTarget(null, null, found) : null;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scope target kind.");
        }
    }
}
