using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>The calendar of one category — every fixture, in chronological order, with its result.</summary>
/// <remarks>
/// Mirrors <c>Matches.ReadMatches</c>'s own ordering and placeholder naming
/// ("Ganador de {phase}" for a knockout slot nobody has filled yet) rather
/// than sharing code with it: its own <c>Ordered</c>/<c>Project</c> are
/// private to that minimal API handler, not factored out the way
/// <see cref="SportFrog.Api.Features.Standings.StandingsQuery"/> is for its
/// two readers. Left out on purpose: the live score a match in progress
/// carries — an export is a snapshot somebody downloads, not a number worth
/// chasing while it still moves.
/// </remarks>
internal sealed class MatchesList : IListProvider
{
    private static readonly Dictionary<MatchState, string> StatusLabels = new()
    {
        [MatchState.Scheduled] = "Programado",
        [MatchState.InProgress] = "En curso",
        [MatchState.Finished] = "Finalizado",
        [MatchState.Postponed] = "Aplazado",
        [MatchState.Walkover] = "Walkover",
        [MatchState.Cancelled] = "Cancelado",
    };

    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Fecha", ListValueKind.DateTime),
        new ListColumn("Local", ListValueKind.Text),
        new ListColumn("Marcador", ListValueKind.Text),
        new ListColumn("Visitante", ListValueKind.Text),
        new ListColumn("Cancha", ListValueKind.Text),
        new ListColumn("Estado", ListValueKind.Text),
    ];

    public string Slug => "partidos";

    public string Label => "Partidos y resultados";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [new ListParameter("categoryId", ListParameterKind.Category, Required: true)];

    // A judged category runs no matches at all — its calendar is a
    // Performances one, scheduled and read through that feature instead.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(scoreMode != ScoreMode.Judged);

    public async Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is not { } categoryId)
        {
            return null;
        }

        var category = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == categoryId)
            .Select(candidate => new { candidate.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var matches = await database.Matches
            .AsNoTracking()
            .Where(match => match.CategoryId == categoryId)

            // Oldest-first, with anything still undated sorting last — the
            // same reading order ReadMatches.Ordered already uses, because
            // that is the order an organizer actually works a calendar in.
            .OrderBy(match => match.ScheduledAt == null)
            .ThenBy(match => match.ScheduledAt)
            .Select(match => new
            {
                match.ScheduledAt,
                match.HomeTeamId,
                HomeTeamName = match.HomeTeam!.Name,
                HomePlaceholder = match.HomeTeamId == null && match.HomeSourceMatch != null
                    ? "Ganador de " + match.HomeSourceMatch.Phase
                    : null,
                match.AwayTeamId,
                AwayTeamName = match.AwayTeam!.Name,
                AwayPlaceholder = match.AwayTeamId == null && match.AwaySourceMatch != null
                    ? "Ganador de " + match.AwaySourceMatch.Phase
                    : null,
                VenueName = match.VenueSpace != null ? match.VenueSpace.Venue!.Name : null,
                SpaceName = match.VenueSpace != null ? match.VenueSpace.Name : null,
                match.HomeTotal,
                match.AwayTotal,
                match.Status,
            })
            .ToListAsync(cancellationToken);

        var rows = matches
            .Select(match => (IReadOnlyList<object?>)
            [
                match.ScheduledAt,
                match.HomeTeamName ?? match.HomePlaceholder ?? "Por definir",
                Score(match.HomeTotal, match.AwayTotal),
                match.AwayTeamName ?? match.AwayPlaceholder ?? "Por definir",
                match.VenueName is { } venue ? $"{venue} — {match.SpaceName}" : "Sin definir",
                StatusLabels.GetValueOrDefault(match.Status, match.Status.ToString()),
            ])
            .ToList();

        return new ListTable($"Partidos — {category.Name}", null, Columns, [new ListSection(null, rows)]);
    }

    private static string Score(int? home, int? away) => home is { } h && away is { } a ? $"{h} - {a}" : "vs";
}
