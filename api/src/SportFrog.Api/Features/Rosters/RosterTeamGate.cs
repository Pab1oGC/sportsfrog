using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>A team open to receive a new registration, or the reason it is not.</summary>
internal sealed record OpenedRosterTeam(IResult? Refusal, Team? Team, Category? Category);

/// <summary>
/// Everything that has to be true about a team before anyone — one person or
/// several — is registered onto its roster.
/// </summary>
/// <remarks>
/// Shared between <see cref="RegisterPlayer"/> and
/// <see cref="RegisterPlayersBulk"/> for the same reason
/// <see cref="Import.RosterImportGate"/> exists: two copies of these checks
/// would drift, and the looser one would be the one that mattered.
/// </remarks>
internal static class RosterTeamGate
{
    /// <summary>
    /// Opens the team, or answers why not. Null means no such team.
    /// </summary>
    public static async Task<OpenedRosterTeam?> OpenAsync(
        Guid teamId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        // Tracked, unlike most reads here: an individual-sport team's name
        // may be rewritten by whoever calls this, and that has to ride along
        // in the same SaveChanges as the registration it is a consequence of.
        var team = await database.Teams
            .Include(candidate => candidate.Category)
                .ThenInclude(category => category!.Competition)
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team?.Category is not { Competition: { } competition } category)
        {
            return null;
        }

        if (competition.Status is CompetitionState.Finished or CompetitionState.Cancelled)
        {
            // Registering into a competition that is over does not add a
            // player to anything; it edits history. Mid-season is left open on
            // purpose — squads change while a league runs, and refusing that
            // would be refusing how the sport works.
            return Conflict("Esta competencia ya terminó, así que sus nóminas están cerradas.");
        }

        if (!team.IsActive)
        {
            return Conflict($"{team.Name} se retiró de esta categoría, así que no está tomando " +
                             "registros.");
        }

        return new OpenedRosterTeam(null, team, category);
    }

    private static OpenedRosterTeam Conflict(string message) =>
        new(Results.Problem(detail: message, statusCode: StatusCodes.Status409Conflict), null, null);
}
