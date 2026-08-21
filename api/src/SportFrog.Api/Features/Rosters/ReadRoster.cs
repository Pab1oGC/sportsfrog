using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>
/// Reads the squad of a team, or one registration.
/// </summary>
public static class ReadRoster
{
    /// <param name="WithdrawnAt">
    /// Set for someone who left the team. They stay in the list because they
    /// played: a squad read for a match already played has to include them,
    /// and a caller showing only who is available filters on this.
    /// </param>
    public sealed record Summary(
        Guid Id,
        Guid TeamId,
        Guid AthleteId,
        string FirstName,
        string LastName,
        string DocumentId,
        DateOnly BirthDate,
        short? JerseyNumber,
        string? Position,
        DateTimeOffset RegisteredAt,
        DateTimeOffset? WithdrawnAt);

    public static IEndpointRouteBuilder MapReadRoster(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/teams/{teamId:guid}/roster", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadRoster))
            .WithSummary("Reads the squad of a team.");

        routes.MapGet("/roster/{id:guid}", ReadAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("ReadRosterEntry")
            .WithSummary("Reads one registration.");

        return routes;
    }

    /// <summary>
    /// The squad, current players first and then those who left.
    /// </summary>
    /// <remarks>
    /// Everyone by default, because the list is read for two different
    /// reasons and only one of them wants a filter: putting out today's team
    /// sheet wants who is available, while reading a match played in March
    /// wants who was registered then. <c>available=true</c> narrows it; the
    /// unnarrowed answer is the complete one.
    /// </remarks>
    private static async Task<IResult> ListAsync(
        Guid teamId,
        SportFrogDbContext database,
        CancellationToken cancellationToken,
        bool available = false)
    {
        if (!await database.Teams.AnyAsync(team => team.Id == teamId, cancellationToken))
        {
            return Results.NotFound();
        }

        return Results.Ok(await Project(database.RosterEntries
                .Where(entry => entry.TeamId == teamId)
                .Where(entry => !available || entry.WithdrawnAt == null)

                // Available first: a list read to pick a team should not start
                // with the people who are gone. Then by shirt, which is how a
                // team sheet is read, with the unnumbered after the numbered.
                .OrderBy(entry => entry.WithdrawnAt != null)
                .ThenBy(entry => entry.JerseyNumber == null)
                .ThenBy(entry => entry.JerseyNumber)
                .ThenBy(entry => entry.Athlete!.LastName))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var entry = await Project(database.RosterEntries.Where(candidate => candidate.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    /// <summary>
    /// Shared so the squad and the single read cannot drift into describing
    /// the same registration differently.
    /// </summary>
    /// <remarks>
    /// The person's details travel with the registration because that is what
    /// a squad list is: a referee checking a team sheet needs a name and a
    /// date of birth, not an identifier to go and look one up with.
    /// </remarks>
    private static IQueryable<Summary> Project(IQueryable<RosterEntry> entries) =>
        entries.Select(entry => new Summary(
            entry.Id,
            entry.TeamId,
            entry.AthleteId,
            entry.Athlete!.FirstName,
            entry.Athlete.LastName,
            entry.Athlete.DocumentId,
            entry.Athlete.BirthDate,
            entry.JerseyNumber,
            entry.Position,
            entry.RegisteredAt,
            entry.WithdrawnAt));
}
