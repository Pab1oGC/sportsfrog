using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Performances;

namespace SportFrog.Api.Features.Reports;

/// <summary>An athlete's file: who they are, and every registration they compete under.</summary>
public static class ReadAthleteReport
{
    public sealed record MatchLine(
        Guid MatchId, DateTimeOffset? ScheduledAt, string OpponentName, bool Home,
        int? OwnTotal, int? OpponentTotal, MatchState Status, string? Outcome);

    public sealed record MetricTotal(string MetricLabel, int Total);

    public sealed record EntryReport(
        Guid RosterEntryId, Guid TeamId, string TeamName, Guid CategoryId, string CategoryName,
        Guid CompetitionId, string CompetitionName, string SportName, bool IsJudged, bool Withdrawn,
        IReadOnlyList<MatchLine> Matches, IReadOnlyList<MetricTotal> Metrics,
        int? Score, PerformanceStatus? PerformanceStatus, int? ClassificationPosition);

    /// <param name="PhotoUrl">
    /// A link to the athlete's own photograph, only when there is a real
    /// one on file — null rather than a link to the placeholder silhouette,
    /// which a screen shows by falling back to its own generic icon instead
    /// of asking for a picture of one. See <see cref="AthleteReportPdf"/>
    /// for where the placeholder actually gets drawn.
    /// </param>
    public sealed record Response(
        Guid AthleteId, string FirstName, string LastName, string DocumentId,
        DateOnly BirthDate, string? Gender, decimal? WeightKg, string? PhotoUrl,
        IReadOnlyList<EntryReport> Entries);

    public static IEndpointRouteBuilder MapReadAthleteReport(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/athletes/{athleteId:guid}/report", HandleJsonAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadAthleteReport))
            .WithSummary("Reads an athlete's report: their file and every registration behind it.");

        routes.MapGet("/athletes/{athleteId:guid}/report.pdf", HandlePdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadAthleteReport) + "Pdf")
            .WithSummary("Renders an athlete's report as a PDF.");

        return routes;
    }

    private static async Task<IResult> HandleJsonAsync(
        Guid athleteId, SportFrogDbContext database, ObjectStore store, AthletePhoto photos,
        CancellationToken cancellationToken)
    {
        var report = await AthleteReportQuery.ForAthleteAsync(database, store, photos, athleteId, cancellationToken);

        return report is null ? Results.NotFound() : Results.Ok(await PresentAsync(report, photos, cancellationToken));
    }

    private static async Task<IResult> HandlePdfAsync(
        Guid athleteId, SportFrogDbContext database, ObjectStore store, AthletePhoto photos,
        CancellationToken cancellationToken)
    {
        var report = await AthleteReportQuery.ForAthleteAsync(database, store, photos, athleteId, cancellationToken);

        return report is null
            ? Results.NotFound()
            : Results.File(AthleteReportPdf.Render(report), "application/pdf", "reporte-deportista.pdf");
    }

    internal static async Task<Response> PresentAsync(
        AthleteReport report, AthletePhoto photos, CancellationToken cancellationToken) => new(
        report.AthleteId, report.FirstName, report.LastName, report.DocumentId,
        report.BirthDate, report.Gender, report.WeightKg,
        await photos.LinkAsync(report.PhotoKey, cancellationToken),
        [.. report.Entries.Select(entry => new EntryReport(
            entry.RosterEntryId, entry.TeamId, entry.TeamName, entry.CategoryId, entry.CategoryName,
            entry.CompetitionId, entry.CompetitionName, entry.SportName, entry.IsJudged, entry.Withdrawn,
            [.. entry.Matches.Select(match => new MatchLine(
                match.MatchId, match.ScheduledAt, match.OpponentName, match.Home,
                match.OwnTotal, match.OpponentTotal, match.Status, match.Outcome))],
            [.. entry.Metrics.Select(metric => new MetricTotal(metric.MetricLabel, metric.Total))],
            entry.Score, entry.PerformanceStatus, entry.ClassificationPosition))]);
}
