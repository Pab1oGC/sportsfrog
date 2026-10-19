using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Staff;

/// <summary>Something a staff registration says that the team does not admit.</summary>
internal sealed record StaffViolation(string Property, string Message);

/// <summary>
/// Whether a person may be on a team's staff, in the role they were given.
///
/// This is the staff counterpart of <c>Features.Rosters.RosterPolicy</c>, and
/// deliberately a much shorter list: none of the category's eligibility rules
/// apply to somebody who does not play. A doctor has no age limit, no weight
/// class and takes none of the squad's places — which is the whole reason
/// <see cref="TeamStaff"/> is not a roster entry carrying a flag.
/// </summary>
/// <remarks>
/// Every rule here is also an index on the table, and that is on purpose: the
/// database is what actually holds two simultaneous requests to one answer,
/// while this exists so the ordinary case is refused with a sentence somebody
/// can act on instead of a unique-violation conflict. Shared between
/// registering, correcting a role and reinstating, which ask the same question
/// of slightly different inputs.
/// </remarks>
internal sealed class StaffPolicy(SportFrogDbContext database)
{
    /// <summary>
    /// Everything wrong with the registration, or nothing.
    /// </summary>
    /// <param name="excluding">
    /// The row being corrected or reinstated, left out of its own comparisons
    /// — without it a staff member would collide with themselves. Absent when
    /// registering somebody new.
    /// </param>
    public async Task<IReadOnlyList<StaffViolation>> InspectAsync(
        Guid teamId,
        Guid athleteId,
        StaffRole role,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var violations = new List<StaffViolation>();

        // Withdrawn rows count here, exactly as uq_team_staff_team_athlete
        // counts them: somebody who worked for this team and left is
        // reinstated, not registered a second time.
        var alreadyOnStaff = await database.TeamStaff
            .AsNoTracking()
            .Where(candidate => candidate.TeamId == teamId && candidate.AthleteId == athleteId)
            .Where(candidate => excluding == null || candidate.Id != excluding)
            .AnyAsync(cancellationToken);

        if (alreadyOnStaff)
        {
            violations.Add(new StaffViolation(
                "AthleteId",
                "Esa persona ya figura en el cuerpo técnico de este equipo. Si se había " +
                "retirado, reincorporala en lugar de registrarla de nuevo."));
        }

        if (role == StaffRole.HeadCoach && await HeadCoachTakenAsync(teamId, excluding, cancellationToken))
        {
            violations.Add(new StaffViolation(
                "Role",
                "Este equipo ya tiene un director técnico. Retirá al actual antes de " +
                "registrar a otro."));
        }

        return violations;
    }

    /// <summary>
    /// Whether somebody is already the team's head coach.
    /// </summary>
    /// <remarks>
    /// Only among those actually working here: a coach who left is history,
    /// and the seat they vacated belongs to whoever takes it — the same
    /// reading uq_team_staff_head_coach makes with its withdrawn_at filter.
    /// </remarks>
    private Task<bool> HeadCoachTakenAsync(
        Guid teamId, Guid? excluding, CancellationToken cancellationToken) =>
        database.TeamStaff
            .AsNoTracking()
            .Where(candidate => candidate.TeamId == teamId
                && candidate.Role == StaffRole.HeadCoach
                && candidate.WithdrawnAt == null)
            .Where(candidate => excluding == null || candidate.Id != excluding)
            .AnyAsync(cancellationToken);

    /// <summary>
    /// The violations as the one answer a caller gets.
    /// </summary>
    /// <remarks>
    /// Every reason at once, and in one place rather than repeated by each of
    /// the three endpoints that can produce them: somebody filling in a bench
    /// should learn that the person is already on it *and* that the role is
    /// taken in one answer, not across two attempts.
    /// </remarks>
    public static IResult Refuse(IReadOnlyList<StaffViolation> violations) =>
        Results.ValidationProblem(violations
            .GroupBy(violation => violation.Property)
            .ToDictionary(
                group => group.Key,
                group => group.Select(violation => violation.Message).ToArray()));
}
