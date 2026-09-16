using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Features.Performances;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Performances;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Reports;

/// <summary>One fixture the athlete's team played, from the athlete's own record.</summary>
internal sealed record AthleteMatchLine(
    Guid MatchId,
    DateTimeOffset? ScheduledAt,
    string OpponentName,
    bool Home,
    int? OwnTotal,
    int? OpponentTotal,
    MatchState Status,
    string? Outcome);

/// <summary>How much of one metric the athlete has recorded, across the whole category.</summary>
internal sealed record AthleteMetricTotal(string MetricLabel, int Total);

/// <summary>
/// One registration's worth of the athlete's file — one team, one category,
/// one competition. An athlete competing in more than one has one of these
/// per registration.
/// </summary>
internal sealed record AthleteEntryReport(
    Guid RosterEntryId,
    Guid TeamId,
    string TeamName,
    Guid CategoryId,
    string CategoryName,
    Guid CompetitionId,
    string CompetitionName,
    string SportName,
    bool IsJudged,

    /// <summary>Left the team mid-competition. See <see cref="RosterEntry.WithdrawnAt"/>.</summary>
    bool Withdrawn,

    IReadOnlyList<AthleteMatchLine> Matches,
    IReadOnlyList<AthleteMetricTotal> Metrics,

    /// <summary>This athlete's team's classification-stage slot, for a judged category. Null otherwise.</summary>
    int? Score,
    PerformanceStatus? PerformanceStatus,
    int? ClassificationPosition,

    /// <summary>
    /// This registration's own competition's mark and colour — see
    /// <see cref="CompetitionBranding"/>. Both null when that competition
    /// never set either, and, across two entries, possibly different each
    /// time: nothing says an athlete's registrations all belong to the same
    /// competition, let alone the same one's look.
    /// </summary>
    byte[]? LogoBytes,
    string? AccentColor);

/// <summary>The athlete's file: who they are, and every registration behind it.</summary>
/// <param name="PhotoBytes">
/// The athlete's own photograph, already read from storage — or the
/// placeholder silhouette when there is no real one on file. Never null:
/// see <see cref="AthletePhoto.BytesAsync"/> for why a missing photograph
/// never leaves this blank.
/// </param>
internal sealed record AthleteReport(
    Guid AthleteId,
    string FirstName,
    string LastName,
    string DocumentId,
    DateOnly BirthDate,
    string? Gender,
    decimal? WeightKg,
    byte[] PhotoBytes,

    /// <summary>
    /// The raw stored key, kept alongside the already-resolved
    /// <see cref="PhotoBytes"/> so a JSON reader can ask
    /// <see cref="AthletePhoto.LinkAsync"/> for a link to the real
    /// photograph — never the placeholder, which is only ever drawn as
    /// pixels, not linked to — without a second trip to the database.
    /// </summary>
    string? PhotoKey,
    IReadOnlyList<AthleteEntryReport> Entries);

/// <summary>
/// Composes an athlete's report from every registration on file for them —
/// genuinely new: nothing before this read an athlete's own record across
/// their matches and metrics. See <see cref="TeamReportQuery"/>'s own
/// remarks for the same point made about a team's.
/// </summary>
internal static class AthleteReportQuery
{
    public static async Task<AthleteReport?> ForAthleteAsync(
        SportFrogDbContext database, ObjectStore store, AthletePhoto photos,
        Guid athleteId, CancellationToken cancellationToken)
    {
        var athlete = await database.Athletes
            .AsNoTracking()
            .Where(candidate => candidate.Id == athleteId)
            .Select(candidate => new
            {
                candidate.OrgId,
                candidate.FirstName,
                candidate.LastName,
                candidate.DocumentId,
                candidate.BirthDate,
                candidate.Gender,
                candidate.WeightKg,
                candidate.PhotoKey,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (athlete is null)
        {
            return null;
        }

        var photoBytes = await photos.BytesAsync(athlete.OrgId, athlete.PhotoKey, cancellationToken);

        var registrations = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.AthleteId == athleteId)
            .Select(entry => new
            {
                entry.Id,
                entry.TeamId,
                TeamName = entry.Team!.Name,
                CategoryId = entry.Team.CategoryId,
                CategoryName = entry.Team.Category!.Name,
                CompetitionId = entry.Team.Category.CompetitionId,
                CompetitionName = entry.Team.Category.Competition!.Name,
                SportCode = entry.Team.Category.Competition.SportCode,
                Withdrawn = entry.WithdrawnAt != null,
            })
            .ToListAsync(cancellationToken);

        var sports = (await database.Sports
                .AsNoTracking()
                .Where(sport => registrations.Select(r => r.SportCode).Contains(sport.Code))
                .ToListAsync(cancellationToken))
            .ToDictionary(sport => sport.Code);

        var entries = new List<AthleteEntryReport>(registrations.Count);

        // Two registrations can name the same competition (two categories of
        // it), and its branding is only ever worth reading once.
        var brandingByCompetition = new Dictionary<Guid, CompetitionBranding>();

        foreach (var registration in registrations)
        {
            if (!sports.TryGetValue(registration.SportCode, out var sport))
            {
                continue;
            }

            if (!brandingByCompetition.TryGetValue(registration.CompetitionId, out var branding))
            {
                branding = await CompetitionBranding.ReadAsync(database, store, registration.CompetitionId, cancellationToken);
                brandingByCompetition[registration.CompetitionId] = branding;
            }

            if (sport.ScoreMode == ScoreMode.Judged)
            {
                var performances = await PerformancesQuery.ForCategoryAsync(database, registration.CategoryId, cancellationToken);
                var ranked = ClassificationRanking.Rank(performances);
                var mine = ranked.FirstOrDefault(item => item.Entry.TeamId == registration.TeamId);

                entries.Add(new AthleteEntryReport(
                    registration.Id, registration.TeamId, registration.TeamName,
                    registration.CategoryId, registration.CategoryName,
                    registration.CompetitionId, registration.CompetitionName, sport.Name,
                    IsJudged: true, registration.Withdrawn, Matches: [], Metrics: [],
                    mine.Entry?.Score, mine.Entry?.Status, mine.Position, branding.LogoBytes, branding.AccentColor));

                continue;
            }

            var matches = await MatchesAsync(database, registration.TeamId, cancellationToken);
            var metrics = await MetricTotalsAsync(
                database, registration.Id, registration.CategoryId, registration.SportCode, cancellationToken);

            entries.Add(new AthleteEntryReport(
                registration.Id, registration.TeamId, registration.TeamName,
                registration.CategoryId, registration.CategoryName,
                registration.CompetitionId, registration.CompetitionName, sport.Name,
                IsJudged: false, registration.Withdrawn, matches, metrics,
                Score: null, PerformanceStatus: null, ClassificationPosition: null, branding.LogoBytes, branding.AccentColor));
        }

        return new AthleteReport(
            athleteId, athlete.FirstName, athlete.LastName, athlete.DocumentId,
            athlete.BirthDate, athlete.Gender, athlete.WeightKg, photoBytes, athlete.PhotoKey, entries);
    }

    private static async Task<List<AthleteMatchLine>> MatchesAsync(
        SportFrogDbContext database, Guid teamId, CancellationToken cancellationToken)
    {
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
                AwayTeamName = match.AwayTeam!.Name,
                match.HomeTotal,
                match.AwayTotal,
                match.Status,
            })
            .ToListAsync(cancellationToken);

        return [.. matches.Select(match =>
        {
            var home = match.HomeTeamId == teamId;
            var ownTotal = home ? match.HomeTotal : match.AwayTotal;
            var opponentTotal = home ? match.AwayTotal : match.HomeTotal;

            var outcome = ownTotal is not null && opponentTotal is not null
                ? ownTotal > opponentTotal ? "Ganado" : ownTotal < opponentTotal ? "Perdido" : "Empatado"
                : null;

            return new AthleteMatchLine(
                match.Id, match.ScheduledAt, home ? match.AwayTeamName : match.HomeTeamName, home,
                ownTotal, opponentTotal, match.Status, outcome);
        })];
    }

    /// <summary>Everything this athlete has personally recorded of every metric their sport ranks.</summary>
    private static async Task<List<AthleteMetricTotal>> MetricTotalsAsync(
        SportFrogDbContext database, Guid rosterEntryId, Guid categoryId, string sportCode, CancellationToken cancellationToken)
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

        var totals = await database.PlayerEvents
            .AsNoTracking()
            .Where(recorded => recorded.RosterEntryId == rosterEntryId)
            .Where(recorded => recorded.Match!.CategoryId == categoryId)
            .Where(recorded => recorded.Match!.Status == MatchState.Finished)
            .GroupBy(recorded => recorded.MetricId)
            .Select(group => new { MetricId = group.Key, Total = group.Sum(recorded => recorded.Quantity) })
            .ToDictionaryAsync(entry => entry.MetricId, entry => entry.Total, cancellationToken);

        return [.. metrics
            .Where(metric => totals.GetValueOrDefault(metric.Id) > 0)
            .Select(metric => new AthleteMetricTotal(metric.Label, totals[metric.Id]))];
    }
}
