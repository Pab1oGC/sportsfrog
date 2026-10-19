using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff;

/// <summary>
/// Reads a team's staff, or one staff registration.
/// </summary>
public static class ReadTeamStaff
{
    /// <param name="WithdrawnAt">
    /// Set for somebody who left the team. They stay in the list because they
    /// were here: a credential printed in March names them, and a caller
    /// showing only who is currently on the bench filters on this.
    /// </param>
    /// <remarks>
    /// The person's name and document travel with the registration, the same
    /// way <c>Features.Rosters.ReadRoster</c> carries them: this is read to
    /// put a bench on paper, and a list of identifiers to go and look names up
    /// with is not that. The date of birth is left out, unlike a squad's —
    /// nothing about a coach is checked against it.
    /// </remarks>
    public sealed record Summary(
        Guid Id,
        Guid TeamId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        string DocumentId,
        StaffRole Role,
        DateTimeOffset RegisteredAt,
        DateTimeOffset? WithdrawnAt);

    public static IEndpointRouteBuilder MapReadTeamStaff(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/teams/{teamId:guid}/staff", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.Team, "teamId"))
            .WithName(nameof(ReadTeamStaff))
            .WithSummary("Reads a team's technical staff and board.");

        routes.MapGet("/staff/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.TeamStaff, "id"))
            .WithName("ReadStaffMember")
            .WithSummary("Reads one staff registration.");

        return routes;
    }

    /// <summary>
    /// The staff, those currently working here first and then those who left.
    /// </summary>
    /// <remarks>
    /// Everybody by default, for the same reason a squad is read that way: the
    /// list answers two different questions and only one of them wants a
    /// filter. <c>active=true</c> narrows it to who is on the bench today; the
    /// unnarrowed answer is the complete one.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid teamId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        bool active = false)
    {
        if (!await database.Teams.AnyAsync(team => team.Id == teamId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.TeamStaff
                .Where(staff => staff.TeamId == teamId)
                .Where(staff => !active || staff.WithdrawnAt == null)

                // Current staff first, then by role rather than by name: a
                // bench is read from the top down, and the enum's own order is
                // that hierarchy — director técnico, asistente, and so on.
                .OrderBy(staff => staff.WithdrawnAt != null)
                .ThenBy(staff => staff.Role)
                .ThenBy(staff => staff.Athlete!.LastName))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var staff = await Project(database.TeamStaff.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return staff is null ? Results.NotFound() : Results.Ok(staff);
    }

    /// <summary>
    /// Shared so the list and the single read cannot drift into describing the
    /// same registration differently.
    /// </summary>
    private static IQueryable<Summary> Project(IQueryable<TeamStaff> staff) =>
        staff.Select(member => new Summary(
            member.Id,
            member.TeamId,
            member.AthleteId,
            member.Athlete!.FirstName,
            member.Athlete.LastName,
            member.Athlete.DocumentId,
            member.Role,
            member.RegisteredAt,
            member.WithdrawnAt));
}
