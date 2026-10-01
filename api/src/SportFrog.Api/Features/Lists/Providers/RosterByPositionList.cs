using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>The squad of whichever team finished in a given place in a category — the champion, the runner-up...</summary>
/// <remarks>
/// Resolves the team through <see cref="ChampionResolver"/> first, then
/// reads its squad through the same <see cref="TeamRosterRows"/>
/// <see cref="RosterList"/> uses — this and that provider never answer "who
/// is on this team" two different ways.
///
/// An unresolved position is not "not found": the category is real, it
/// simply has nothing to say for that place yet (a final not played, a
/// league not started). That reads as an empty list carrying its own reason
/// in <see cref="ListTable.Subtitle"/>, the same honesty
/// <c>CardsList</c> already applies to a sport with no card metrics —
/// "not found" is reserved for a category id that names nothing at all.
/// </remarks>
internal sealed class RosterByPositionList : IListProvider
{
    public string Slug => "plantel-puesto";

    public string Label => "Plantel del campeón, subcampeón...";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [
            new ListParameter("categoryId", ListParameterKind.Category, Required: true),
            new ListParameter("position", ListParameterKind.Position, Required: true),
        ];

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is not { } categoryId || scope.GetInt("position") is not { } position)
        {
            return null;
        }

        var resolution = await ChampionResolver.ResolveAsync(database, categoryId, position, cancellationToken);

        if (resolution.Resolution == ChampionResolution.CategoryNotFound)
        {
            return null;
        }

        var title = $"Plantel del {PositionLabel(position)} — {resolution.CategoryName}";

        if (resolution.Resolution == ChampionResolution.Undecided)
        {
            return new ListTable(title, resolution.Reason, TeamRosterRows.Columns, []);
        }

        if (await TeamRosterRows.ForTeamAsync(database, resolution.TeamId!.Value, cancellationToken) is not { } squad)
        {
            // The position resolved to a team that no longer exists — a
            // dangling reference rather than a reason to 404 the whole
            // category, so this reads the same as any other "nothing to
            // show yet".
            return new ListTable(
                title, "El equipo que tenía ese puesto ya no está disponible.", TeamRosterRows.Columns, []);
        }

        return new ListTable(title, null, TeamRosterRows.Columns, [new ListSection(null, squad.Rows)]);
    }

    private static string PositionLabel(int position) => position switch
    {
        1 => "campeón",
        2 => "subcampeón",
        3 => "tercer puesto",
        _ => $"{position}° puesto",
    };
}
