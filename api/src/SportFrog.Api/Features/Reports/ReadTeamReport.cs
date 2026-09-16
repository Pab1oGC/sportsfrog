using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Performances;

namespace SportFrog.Api.Features.Reports;

/// <summary>A team's report: its record, its fixtures, and its own players' place in the category's boards.</summary>
public static class ReadTeamReport
{
    public sealed record MatchLine(
        Guid MatchId, DateTimeOffset? ScheduledAt, string OpponentName, bool Home,
        int? OwnTotal, int? OpponentTotal, MatchState Status, string? Outcome);

    public sealed record LeaderLine(string MetricLabel, string AthleteName, short? JerseyNumber, int Total);

    /// <param name="Standing">
    /// Null for a judged category — see <see cref="Score"/> instead — or for
    /// a team that has not played into a group's own table at all.
    /// </param>
    public sealed record Response(
        Guid TeamId,
        string TeamName,
        string? ClubName,
        Guid CategoryId,
        string CategoryName,
        Guid CompetitionId,
        string CompetitionName,
        string SportName,
        bool IsJudged,
        StandingLine? Standing,
        IReadOnlyList<MatchLine> Matches,
        IReadOnlyList<LeaderLine> Leaders,
        int? Score,
        PerformanceStatus? PerformanceStatus,
        int? ClassificationPosition);

    public sealed record StandingLine(
        int Position, int Played, int Won, int Drawn, int Lost,
        int ScoreFor, int ScoreAgainst, int ScoreDifference, int Points, string? GroupLabel);

    public static IEndpointRouteBuilder MapReadTeamReport(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/teams/{teamId:guid}/report", HandleJsonAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadTeamReport))
            .WithSummary("Reads a team's report: its record, fixtures and own leaders.");

        routes.MapGet("/teams/{teamId:guid}/report.pdf", HandlePdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadTeamReport) + "Pdf")
            .WithSummary("Renders a team's report as a PDF.");

        return routes;
    }

    private static async Task<IResult> HandleJsonAsync(
        Guid teamId, SportFrogDbContext database, ObjectStore store, CancellationToken cancellationToken)
    {
        var report = await TeamReportQuery.ForTeamAsync(database, store, teamId, cancellationToken);

        return report is null ? Results.NotFound() : Results.Ok(Present(report));
    }

    private static async Task<IResult> HandlePdfAsync(
        Guid teamId, SportFrogDbContext database, ObjectStore store, CancellationToken cancellationToken)
    {
        var report = await TeamReportQuery.ForTeamAsync(database, store, teamId, cancellationToken);

        return report is null
            ? Results.NotFound()
            : Results.File(TeamReportPdf.Render(report), "application/pdf", "reporte-equipo.pdf");
    }

    internal static Response Present(TeamReport report) => new(
        report.TeamId, report.TeamName, report.ClubName, report.CategoryId, report.CategoryName,
        report.CompetitionId, report.CompetitionName, report.SportName, report.IsJudged,
        report.Standing is { } standing && report.StandingsPosition is { } position
            ? new StandingLine(
                position, standing.Played, standing.Won, standing.Drawn, standing.Lost,
                standing.ScoreFor, standing.ScoreAgainst, standing.ScoreDifference, standing.Points, standing.GroupLabel)
            : null,
        [.. report.Matches.Select(match => new MatchLine(
            match.MatchId, match.ScheduledAt, match.OpponentName, match.Home,
            match.OwnTotal, match.OpponentTotal, match.Status, match.Outcome))],
        [.. report.Leaders.Select(leader => new LeaderLine(
            leader.MetricLabel, leader.AthleteName, leader.JerseyNumber, leader.Total))],
        report.Score,
        report.PerformanceStatus,
        report.ClassificationPosition);
}
