using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>The squad of one team, current players first and then those who left.</summary>
/// <remarks>
/// A thin wrapper over <see cref="TeamRosterRows"/> — the team is already
/// known here, named directly in the scope, unlike
/// <see cref="RosterByPositionList"/>'s, which has to be resolved first.
/// </remarks>
internal sealed class RosterList : IListProvider
{
    public string Slug => "plantel";

    public string Label => "Plantel de un equipo";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("teamId", ListParameterKind.Team, Required: true)];

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("teamId") is not { } teamId)
        {
            return null;
        }

        if (await TeamRosterRows.ForTeamAsync(database, teamId, cancellationToken) is not { } squad)
        {
            return null;
        }

        return new ListTable(
            $"Plantel — {squad.TeamName}", null, TeamRosterRows.Columns, [new ListSection(null, squad.Rows)]);
    }
}
