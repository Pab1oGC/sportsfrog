using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>One team's squad, shaped as list rows — current players first, then those who left.</summary>
/// <remarks>
/// Shared by every provider that ends in "a team's roster", whichever way it
/// got to that team: <see cref="RosterList"/> is handed one directly,
/// <see cref="RosterByPositionList"/> resolves one through
/// <see cref="ChampionResolver"/> first. Two copies of this query would mean
/// the two could answer "who is on this team" differently the day one of
/// them changes — the same reason <c>StandingsQuery</c> and
/// <c>LeadersQuery</c> are shared by their own two readers.
///
/// Mirrors <c>Rosters.ReadRoster.ListAsync</c>'s own filter and order rather
/// than sharing code with it: that query is private to its own minimal API
/// handler, not factored out the way this one is for its two readers here.
/// No photo link is resolved — an export reads as text, so nothing here
/// depends on the <c>ObjectStore</c> the screen's own reading needs for one.
/// </remarks>
internal static class TeamRosterRows
{
    public static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Dorsal", ListValueKind.Text),
        new ListColumn("Apellido y nombre", ListValueKind.Text),
        new ListColumn("Documento", ListValueKind.Text),
        new ListColumn("Nacimiento", ListValueKind.Date),
        new ListColumn("Posición", ListValueKind.Text),
        new ListColumn("Retirado", ListValueKind.Boolean),
    ];

    /// <summary>The team's name and its squad's rows, or null when no team has this id.</summary>
    public static async Task<(string TeamName, IReadOnlyList<IReadOnlyList<object?>> Rows)?> ForTeamAsync(
        SportFrogDbContext database, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId)
            .Select(candidate => new { candidate.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return null;
        }

        var entries = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.TeamId == teamId)
            .OrderBy(entry => entry.WithdrawnAt != null)
            .ThenBy(entry => entry.JerseyNumber == null)
            .ThenBy(entry => entry.JerseyNumber)
            .ThenBy(entry => entry.Athlete!.LastName)
            .Select(entry => new
            {
                entry.JerseyNumber,
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.DocumentId,
                entry.Athlete.BirthDate,
                entry.Position,
                Withdrawn = entry.WithdrawnAt != null,
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<IReadOnlyList<object?>> rows =
        [
            .. entries.Select(entry => (IReadOnlyList<object?>)
            [
                entry.JerseyNumber?.ToString() ?? "-",
                $"{entry.LastName}, {entry.FirstName}",
                entry.DocumentId,
                entry.BirthDate,
                entry.Position ?? "-",
                entry.Withdrawn,
            ]),
        ];

        return (team.Name, rows);
    }
}
