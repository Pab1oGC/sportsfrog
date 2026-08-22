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
    public async Task<IReadOnlyList<FixtureViolation>> InspectAsync(
        Guid categoryId,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? venueSpaceId,
        CancellationToken cancellationToken)
    {
        var violations = new List<FixtureViolation>();

        if (homeTeamId == awayTeamId)
        {
            // Reported first and alone: everything below would repeat itself
            // about a single team, and the caller has one thing to fix.
            return [new FixtureViolation("AwayTeamId", "A team cannot play itself.")];
        }

        await InspectTeamAsync(categoryId, homeTeamId, "HomeTeamId", violations, cancellationToken);
        await InspectTeamAsync(categoryId, awayTeamId, "AwayTeamId", violations, cancellationToken);

        await InspectSpaceAsync(venueSpaceId, violations, cancellationToken);

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
                property, "No team of this organization has that identifier."));
            return;
        }

        if (team.CategoryId != categoryId)
        {
            violations.Add(new FixtureViolation(
                property,
                $"{team.Name} is entered in a different category. A fixture is played between " +
                "two teams of the same division."));
            return;
        }

        if (!team.IsActive)
        {
            // A team that withdrew keeps the matches it already played and
            // stops being given new ones. Scheduling it again would undo the
            // withdrawal without anyone deciding to.
            violations.Add(new FixtureViolation(
                property, $"{team.Name} has withdrawn from this category, so it is not scheduled."));
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
                "VenueSpaceId", "No space of this organization has that identifier."));
            return;
        }

        if (!space.Available)
        {
            violations.Add(new FixtureViolation(
                "VenueSpaceId",
                $"{space.VenueName} / {space.Name} is not available, so nothing can be scheduled " +
                "on it."));
        }
    }
}
