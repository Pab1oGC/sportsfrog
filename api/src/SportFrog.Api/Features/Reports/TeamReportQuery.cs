using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Features.Standings;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Performances;
using SportFrog.Domain.Rules;
using SportFrog.Domain.Standings;
using SportFrog.Domain.Statistics;

namespace SportFrog.Api.Features.Reports;

/// <summary>One fixture this team was part of, as the report tells it.</summary>
internal sealed record TeamMatchLine(
    Guid MatchId,
    DateTimeOffset? ScheduledAt,
    string OpponentName,
    bool Home,
    int? OwnTotal,
    int? OpponentTotal,
    MatchState Status,

    /// <summary>"Ganado", "Empatado" or "Perdido" once there is a result to read one from — null otherwise.</summary>
    string? Outcome);

/// <summary>One of the team's own players, leading one of the category's metrics — among its own roster only.</summary>
internal sealed record TeamLeaderLine(string MetricLabel, string AthleteName, short? JerseyNumber, int Total);

/// <summary>Everything a team's report says about it.</summary>
internal sealed record TeamReport(
    Guid TeamId,
    string TeamName,
    string? ClubName,
    Guid CategoryId,
    string CategoryName,
    Guid CompetitionId,
    string CompetitionName,
    string SportName,
    bool IsJudged,

    /// <summary>
    /// Where this team stands in its group, and its position within it —
    /// null for a judged category, which is ranked by
    /// <see cref="Performance"/> instead of by table.
    /// </summary>
    StandingsRow? Standing,
    int? StandingsPosition,
    IReadOnlyList<TeamMatchLine> Matches,
    IReadOnlyList<TeamLeaderLine> Leaders,

    /// <summary>This team's own classification-stage slot, for a judged category. Null otherwise.</summary>
    int? Score,
    PerformanceStatus? PerformanceStatus,
    int? ClassificationPosition,

    /// <summary>The competition's own mark and colour. See <see cref="CompetitionBranding"/>.</summary>
    byte[]? LogoBytes,
    string? AccentColor);

/// <summary>
/// Composes a team's report: its record, its fixtures, and its own players'
/// standing within the category's boards.
/// </summary>
/// <remarks>
/// Genuinely new statistics, not a re-export of an existing screen: nothing
/// in the system before this read a category's tables, boards or
/// classification scoped down to one team — <see cref="Standings.StandingsQuery"/>
/// and <see cref="Statistics.LeadersQuery"/> both answer for a whole category,
/// and a team's own report has to pick itself out of what they compute.
/// </remarks>
internal static class TeamReportQuery
{
    public static async Task<TeamReport?> ForTeamAsync(
        SportFrogDbContext database, ObjectStore store, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Where(candidate => candidate.Id == teamId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                ClubName = candidate.Club!.Name,
                candidate.CategoryId,
                CategoryName = candidate.Category!.Name,
                CompetitionId = candidate.Category.CompetitionId,
                CompetitionName = candidate.Category.Competition!.Name,
                SportCode = candidate.Category.Competition.SportCode,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return null;
        }

        var sport = await database.Sports
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == team.SportCode, cancellationToken);

        if (sport is null)
        {
            return null;
        }

        var branding = await CompetitionBranding.ReadAsync(database, store, team.CompetitionId, cancellationToken);

        if (sport.ScoreMode == ScoreMode.Judged)
        {
            var entries = await PerformancesQuery.ForCategoryAsync(database, team.CategoryId, cancellationToken);
            var ranked = ClassificationRanking.Rank(entries);
            var mine = ranked.FirstOrDefault(item => item.Entry.TeamId == teamId);

            return new TeamReport(
                team.Id, team.Name, team.ClubName, team.CategoryId, team.CategoryName,
                team.CompetitionId, team.CompetitionName, sport.Name, IsJudged: true,
                Standing: null, StandingsPosition: null, Matches: [], Leaders: [],
                mine.Entry?.Score, mine.Entry?.Status, mine.Position, branding.LogoBytes, branding.AccentColor);
        }

        var standings = await StandingsQuery.ForCategoryAsync(database, team.CategoryId, cancellationToken);
        StandingsRow? standing = null;
        int? position = null;

        if (standings is not null)
        {
            foreach (var group in standings.Groups)
            {
                var index = group.Rows.ToList().FindIndex(row => row.TeamId == teamId);

                if (index >= 0)
                {
                    standing = group.Rows[index];
                    position = index + 1;
                    break;
                }
            }
        }

        var matches = await database.Matches
            .AsNoTracking()
            .Where(match => match.HomeTeamId == teamId || match.AwayTeamId == teamId)
            .Where(match => match.Status != MatchState.Cancelled)
            .OrderBy(match => match.ScheduledAt == null)
            .ThenBy(match => match.ScheduledAt)
            .Select(match => new
            {
                match.Id,
                match.ScheduledAt,
                match.HomeTeamId,
                HomeTeamName = match.HomeTeam!.Name,
                match.AwayTeamId,
                AwayTeamName = match.AwayTeam!.Name,
                match.HomeTotal,
                match.AwayTotal,
                match.Status,
            })
            .ToListAsync(cancellationToken);

        var lines = matches.Select(match =>
        {
            var home = match.HomeTeamId == teamId;
            var ownTotal = home ? match.HomeTotal : match.AwayTotal;
            var opponentTotal = home ? match.AwayTotal : match.HomeTotal;

            var outcome = ownTotal is not null && opponentTotal is not null
                ? ownTotal > opponentTotal ? "Ganado" : ownTotal < opponentTotal ? "Perdido" : "Empatado"
                : null;

            return new TeamMatchLine(
                match.Id, match.ScheduledAt, home ? match.AwayTeamName : match.HomeTeamName, home,
                ownTotal, opponentTotal, match.Status, outcome);
        }).ToList();

        var leaders = await OwnLeadersAsync(database, teamId, team.CategoryId, team.SportCode, cancellationToken);

        return new TeamReport(
            team.Id, team.Name, team.ClubName, team.CategoryId, team.CategoryName,
            team.CompetitionId, team.CompetitionName, sport.Name, IsJudged: false,
            standing, position, lines, leaders,
            Score: null, PerformanceStatus: null, ClassificationPosition: null, branding.LogoBytes, branding.AccentColor);
    }

    private sealed record PlayerTotal(Guid MetricId, string FirstName, string LastName, short? JerseyNumber, int Total);

    /// <summary>
    /// The team's own players, ranked within their own roster only, one line
    /// per metric the sport ranks and only has recorded — the same tallying
    /// <see cref="Statistics.LeadersQuery"/> does for the whole category,
    /// scoped down to one team's roster entries.
    /// </summary>
    private static async Task<List<TeamLeaderLine>> OwnLeadersAsync(
        SportFrogDbContext database, Guid teamId, Guid categoryId, string sportCode, CancellationToken cancellationToken)
    {
        var metrics = await database.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == sportCode && metric.IsRankable)
            .OrderBy(metric => metric.DisplayOrder)
            .Select(metric => new { metric.Id, metric.Label })
            .ToListAsync(cancellationToken);

        if (metrics.Count == 0)
        {
            return [];
        }

        var tallies = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.RosterEntry!.TeamId == teamId)
            .Where(recorded => recorded.Match!.CategoryId == categoryId)
            .Where(recorded => recorded.Match!.Status == MatchState.Finished)
            .GroupBy(recorded => new
            {
                recorded.MetricId,
                recorded.RosterEntry!.Athlete!.FirstName,
                recorded.RosterEntry.Athlete.LastName,
                recorded.RosterEntry.JerseyNumber,
            })
            .Select(group => new PlayerTotal(
                group.Key.MetricId,
                group.Key.FirstName,
                group.Key.LastName,
                group.Key.JerseyNumber,
                group.Sum(recorded => recorded.Quantity)))
            .ToListAsync(cancellationToken);

        var byMetric = tallies.ToLookup(tally => tally.MetricId);

        var lines = new List<TeamLeaderLine>();

        foreach (var metric in metrics)
        {
            var top = byMetric[metric.Id].OrderByDescending(tally => tally.Total).FirstOrDefault();

            if (top is not null && top.Total > 0)
            {
                lines.Add(new TeamLeaderLine(metric.Label, $"{top.FirstName} {top.LastName}", top.JerseyNumber, top.Total));
            }
        }

        return lines;
    }
}
