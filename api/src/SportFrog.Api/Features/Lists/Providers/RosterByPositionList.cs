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

    // ChampionResolver already answers "who finished Nth" for every score
    // mode — a bracket, a table, or a judged classification — so this has
    // somewhere to look regardless of which one a category uses.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(true);

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

        // Neither of these two rows-less cases has resolved a team to read
        // IsIndividual from, and neither carries a single row for a wrong
        // "Dorsal"/"Posición" column to actually mislead — so the full
        // column set is the simpler, equally honest choice here rather than
        // a second query just to label an empty table correctly.
        if (resolution.Resolution == ChampionResolution.Undecided)
        {
            return new ListTable(title, resolution.Reason, TeamRosterRows.Columns(isIndividual: false), []);
        }

        if (await TeamRosterRows.ForTeamAsync(database, resolution.TeamId!.Value, cancellationToken) is not { } squad)
        {
            // The position resolved to a team that no longer exists — a
            // dangling reference rather than a reason to 404 the whole
            // category, so this reads the same as any other "nothing to
            // show yet".
            return new ListTable(
                title, "El equipo que tenía ese puesto ya no está disponible.",
                TeamRosterRows.Columns(isIndividual: false), []);
        }

        return new ListTable(
            title, null, TeamRosterRows.Columns(squad.IsIndividual), [new ListSection(null, squad.Rows)]);
    }

    private static string PositionLabel(int position) => position switch
    {
        1 => "campeón",
        2 => "subcampeón",
        3 => "tercer puesto",
        _ => $"{position}° puesto",
    };
}
