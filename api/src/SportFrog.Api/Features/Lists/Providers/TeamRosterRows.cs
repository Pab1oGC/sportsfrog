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
    /// <summary>
    /// A squad's columns — "Dorsal" and "Posición" only for a team sport.
    /// An individual sport's entrant never gets either: <c>EnrollIndividual</c>
    /// is the only way onto a team shaped like one, and it never collects a
    /// jersey number or an on-field position for an athlete, a pair, or a
    /// poomsae trio — showing the column would just be a "-" on every row,
    /// the exact kind of football-shaped noise this export should not carry
    /// into a taekwondo one.
    /// </summary>
    // internal, not private: testable directly without a database, the same
    // convention StandingsList.Columns above already uses for a column set
    // that depends on a fact about the team rather than being fixed.
    internal static IReadOnlyList<ListColumn> Columns(bool isIndividual)
    {
        List<ListColumn> columns = [];

        if (!isIndividual)
        {
            columns.Add(new ListColumn("Dorsal", ListValueKind.Text));
        }

        columns.Add(new ListColumn("Apellido y nombre", ListValueKind.Text));
        columns.Add(new ListColumn("Documento", ListValueKind.Text));
        columns.Add(new ListColumn("Nacimiento", ListValueKind.Date));

        if (!isIndividual)
        {
            columns.Add(new ListColumn("Posición", ListValueKind.Text));
        }

        columns.Add(new ListColumn("Retirado", ListValueKind.Boolean));

        return columns;
    }

    /// <summary>The team's name, whether it is an individual entrant, and its squad's rows — or null when no team has this id.</summary>
    public static async Task<(string TeamName, bool IsIndividual, IReadOnlyList<IReadOnlyList<object?>> Rows)?> ForTeamAsync(
        SportFrogDbContext database, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId)
            .Select(candidate => new { candidate.Name, candidate.IsIndividual })
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
            .. entries.Select(entry =>
            {
                List<object?> cells = [];

                if (!team.IsIndividual)
                {
                    cells.Add(entry.JerseyNumber?.ToString() ?? "-");
                }

                cells.Add($"{entry.LastName}, {entry.FirstName}");
                cells.Add(entry.DocumentId);
                cells.Add(entry.BirthDate);

                if (!team.IsIndividual)
                {
                    cells.Add(entry.Position ?? "-");
                }

                cells.Add(entry.Withdrawn);

                return (IReadOnlyList<object?>)cells;
            }),
        ];

        return (team.Name, team.IsIndividual, rows);
    }
}
