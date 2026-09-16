using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Matches;

/// <summary>Something a fixture says that its category does not allow.</summary>
internal sealed record FixtureViolation(string Property, string Message);

/// <summary>
/// Whether two teams can be put on a calendar against each other, at a place
/// that can hold them.
/// </summary>
/// <remarks>
/// The schema settles one of these on its own — <c>ck_match_distinct_teams</c>
/// refuses a side playing itself — and it is restated here anyway, because a
/// constraint violation surfaces as a database error naming a constraint, and
/// the caller needs a sentence naming a field.
///
/// The rest the schema cannot see. A foreign key can say the home team
/// exists; it cannot say the team belongs to <em>this</em> category, and a
/// fixture between a Sub-15 side and a senior one would satisfy every key on
/// the table.
/// </remarks>
internal sealed class FixturePolicy(SportFrogDbContext database)
{
    /// <param name="scheduledAt">
    /// When given, also checked against every other live fixture for a
    /// collision — same ground, same teams — at that exact instant. Omitted
    /// entirely when null: a fixture with a date to decide later is
    /// ordinary, and has nothing yet to collide with.
    /// </param>
    /// <param name="excludingMatchId">
    /// The fixture being corrected, left out of its own collision check —
    /// otherwise a match would always collide with itself the moment
    /// <see cref="RescheduleMatch"/> re-inspects it unchanged.
    /// Null when scheduling a new one.
    /// </param>
    public async Task<IReadOnlyList<FixtureViolation>> InspectAsync(
        Guid categoryId,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? venueSpaceId,
        DateTimeOffset? scheduledAt,
        Guid? excludingMatchId,
        CancellationToken cancellationToken)
    {
        var violations = new List<FixtureViolation>();

        if (homeTeamId == awayTeamId)
        {
            // Reported first and alone: everything below would repeat itself
            // about a single team, and the caller has one thing to fix.
            return [new FixtureViolation("AwayTeamId", "Un equipo no puede jugar contra sí mismo.")];
        }

        await InspectTeamAsync(categoryId, homeTeamId, "HomeTeamId", violations, cancellationToken);
        await InspectTeamAsync(categoryId, awayTeamId, "AwayTeamId", violations, cancellationToken);

        await InspectSpaceAsync(venueSpaceId, violations, cancellationToken);

        if (scheduledAt is { } at)
        {
            await InspectCollisionsAsync(
                homeTeamId, awayTeamId, venueSpaceId, at, excludingMatchId, violations, cancellationToken);
        }

        return violations;
    }

    /// <summary>
    /// The team has to exist, belong to this category, and still be competing.
    /// </summary>
    private async Task InspectTeamAsync(
        Guid categoryId,
        Guid teamId,
        string property,
        List<FixtureViolation> violations,
        CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId)
            .Select(candidate => new { candidate.Name, candidate.CategoryId, candidate.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            violations.Add(new FixtureViolation(
                property, "Ningún equipo de esta organización tiene ese identificador."));
            return;
        }

        if (team.CategoryId != categoryId)
        {
            violations.Add(new FixtureViolation(
                property,
                $"{team.Name} está inscripto en otra categoría. Un partido se juega entre " +
                "dos equipos de la misma división."));
            return;
        }

        if (!team.IsActive)
        {
            // A team that withdrew keeps the matches it already played and
            // stops being given new ones. Scheduling it again would undo the
            // withdrawal without anyone deciding to.
            violations.Add(new FixtureViolation(
                property, $"{team.Name} se retiró de esta categoría, así que no se le programa."));
        }
    }

    /// <summary>
    /// The ground has to exist and be usable.
    /// </summary>
    /// <remarks>
    /// Availability is both flags: a space of a deactivated venue is not
    /// offered, which is the same rule the venues module answers with
    /// <c>isAvailable</c>. Checked only when a space is given — a fixture with
    /// a date and no pitch yet is ordinary.
    /// </remarks>
    private async Task InspectSpaceAsync(
        Guid? venueSpaceId,
        List<FixtureViolation> violations,
        CancellationToken cancellationToken)
    {
        if (venueSpaceId is not { } spaceId)
        {
            return;
        }

        var space = await database.VenueSpaces
            .AsNoTracking()
            .Where(candidate => candidate.Id == spaceId)
            .Select(candidate => new
            {
                candidate.Name,
                VenueName = candidate.Venue!.Name,
                Available = candidate.IsActive && candidate.Venue.IsActive,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (space is null)
        {
            violations.Add(new FixtureViolation(
                "VenueSpaceId", "Ningún espacio de esta organización tiene ese identificador."));
            return;
        }

        if (!space.Available)
        {
            violations.Add(new FixtureViolation(
                "VenueSpaceId",
                $"{space.VenueName} / {space.Name} no está disponible, así que no se puede " +
                "programar nada ahí."));
        }
    }

    /// <summary>
    /// Two things cannot be in two places at the same instant: the ground,
    /// and either team — not scoped to one competition, because neither can
    /// a real ground or a real competitor.
    /// </summary>
    /// <remarks>
    /// The ground already has the last word at the schema — <c>uq_space_schedule</c>
    /// refuses the same space and instant twice, regardless of what runs
    /// here first. This is not a replacement for that: it is what turns the
    /// constraint's generic rejection into a sentence naming the fixture
    /// actually in the way, for the ordinary case where two requests are not
    /// racing each other. A team has no constraint backing it at all — nothing
    /// in the schema can see that two different matches share a team — so for
    /// that half this check is the only one there is.
    /// </remarks>
    private async Task InspectCollisionsAsync(
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? venueSpaceId,
        DateTimeOffset scheduledAt,
        Guid? excludingMatchId,
        List<FixtureViolation> violations,
        CancellationToken cancellationToken)
    {
        if (venueSpaceId is not null)
        {
            var atSpace = await database.Matches
                .AsNoTracking()
                .Where(candidate => candidate.Id != excludingMatchId)
                .Where(candidate => candidate.VenueSpaceId == venueSpaceId && candidate.ScheduledAt == scheduledAt)
                .Where(candidate => candidate.Status != MatchState.Cancelled && candidate.Status != MatchState.Postponed)
                .Select(candidate => new { HomeTeamName = candidate.HomeTeam!.Name, AwayTeamName = candidate.AwayTeam!.Name })
                .FirstOrDefaultAsync(cancellationToken);

            if (atSpace is not null)
            {
                violations.Add(new FixtureViolation(
                    "VenueSpaceId",
                    $"Esa cancha ya tiene programado a {atSpace.HomeTeamName} vs {atSpace.AwayTeamName} a esa hora."));
            }
        }

        await InspectTeamCollisionAsync(homeTeamId, "HomeTeamId", scheduledAt, excludingMatchId, violations, cancellationToken);
        await InspectTeamCollisionAsync(awayTeamId, "AwayTeamId", scheduledAt, excludingMatchId, violations, cancellationToken);
    }

    /// <summary>
    /// Whether this team — or, failing that, anyone currently on its roster —
    /// is already down for another live fixture at this exact instant,
    /// anywhere in the organization.
    /// </summary>
    /// <remarks>
    /// Two different checks, not one, because a team is scoped to a single
    /// category: the same person entered into a second category, or a second
    /// competition entirely, plays under a second, unrelated <c>Team</c> row.
    /// Comparing team ids alone would never see that it is the same person —
    /// this is what makes an athlete double-booked on two different grounds
    /// visible even though the two fixtures do not appear to share a team at
    /// all. Not competition-scoped for the same reason: a competitor cannot
    /// be in two places at once regardless of which competition either
    /// fixture belongs to.
    /// </remarks>
    private async Task InspectTeamCollisionAsync(
        Guid teamId,
        string property,
        DateTimeOffset scheduledAt,
        Guid? excludingMatchId,
        List<FixtureViolation> violations,
        CancellationToken cancellationToken)
    {
        var sameTeam = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id != excludingMatchId)
            .Where(candidate => candidate.ScheduledAt == scheduledAt)
            .Where(candidate => candidate.Status != MatchState.Cancelled && candidate.Status != MatchState.Postponed)
            .Where(candidate => candidate.HomeTeamId == teamId || candidate.AwayTeamId == teamId)
            .Select(candidate => new { HomeTeamName = candidate.HomeTeam!.Name, AwayTeamName = candidate.AwayTeam!.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (sameTeam is not null)
        {
            violations.Add(new FixtureViolation(
                property, $"Ya está jugando contra {sameTeam.HomeTeamName} vs {sameTeam.AwayTeamName} a esa hora."));
            return;
        }

        // Currently competing, not merely ever registered: someone who left
        // this team mid-competition freed their place on it, and should not
        // still block it from playing.
        var myAthleteIds = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.TeamId == teamId && entry.WithdrawnAt == null)
            .Select(entry => entry.AthleteId)
            .ToListAsync(cancellationToken);

        if (myAthleteIds.Count == 0)
        {
            return;
        }

        var sharedAthlete = await database.Matches
            .AsNoTracking()
            .Where(candidate => candidate.Id != excludingMatchId)
            .Where(candidate => candidate.ScheduledAt == scheduledAt)
            .Where(candidate => candidate.Status != MatchState.Cancelled && candidate.Status != MatchState.Postponed)
            .Where(candidate => database.RosterEntries.Any(entry =>
                (entry.TeamId == candidate.HomeTeamId || entry.TeamId == candidate.AwayTeamId)
                && entry.WithdrawnAt == null
                && myAthleteIds.Contains(entry.AthleteId)))
            .Select(candidate => new { HomeTeamName = candidate.HomeTeam!.Name, AwayTeamName = candidate.AwayTeam!.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (sharedAthlete is not null)
        {
            violations.Add(new FixtureViolation(
                property,
                $"Uno de sus deportistas ya está jugando en otra inscripción contra " +
                $"{sharedAthlete.HomeTeamName} vs {sharedAthlete.AwayTeamName} a esa hora."));
        }
    }
}
