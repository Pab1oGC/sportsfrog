using AwesomeAssertions;
using SportFrog.Api.Features.Reports;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Performances;

namespace SportFrog.Api.Tests.Features.Reports;

/// <summary>
/// QuestPDF's own layout checks only run inside <c>GeneratePdf()</c> — see
/// <c>CompetitionBulletinPdfTests</c>'s own remarks for why this is the only
/// thing that would catch a composition mistake before an organizer does.
/// </summary>
public sealed class ReportPdfTests
{
    // Un PNG de 1x1, minimo -- alcanza para que QuestPDF lo decodifique de
    // verdad al generar.
    private static readonly byte[] TinyLogo = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static readonly TeamMatchLine WonMatch = new(
        Guid.NewGuid(), new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        "Equipo B", true, 2, 1, MatchState.Finished, "Ganado");

    private static TeamReport TeamSportReport(
        IReadOnlyList<TeamMatchLine>? matches = null, IReadOnlyList<TeamLeaderLine>? leaders = null,
        byte[]? logoBytes = null, string? accentColor = null) => new(
        Guid.NewGuid(), "Equipo A", "Club A", Guid.NewGuid(), "Primera", Guid.NewGuid(), "Copa Apertura",
        "Fútbol", IsJudged: false,
        new SportFrog.Domain.Standings.StandingsRow { TeamId = Guid.NewGuid(), TeamName = "Equipo A", Played = 1, Won = 1, Points = 3, ScoreFor = 2, ScoreAgainst = 1 },
        1, matches ?? [WonMatch], leaders ?? [new TeamLeaderLine("Gol", "Juan Pérez", 9, 2)],
        null, null, null, logoBytes, accentColor);

    private static TeamReport JudgedTeamReport() => new(
        Guid.NewGuid(), "Poomsae Individual", null, Guid.NewGuid(), "Poomsae Sub-15", Guid.NewGuid(), "Copa Poomsae",
        "Taekwondo (Poomsae)", IsJudged: true,
        null, null, [], [], 765, PerformanceStatus.Scored, 2, LogoBytes: null, AccentColor: null);

    [Fact]
    public void TeamReportPdf_TeamSport_ProducesAPdf()
    {
        var bytes = TeamReportPdf.Render(TeamSportReport());

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void TeamReportPdf_JudgedCategory_ProducesAPdf()
    {
        var bytes = TeamReportPdf.Render(JudgedTeamReport());

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void TeamReportPdf_NoStandingOrMatchesYet_ProducesAPdf()
    {
        var report = TeamSportReport(matches: [], leaders: []) with { Standing = null, StandingsPosition = null };

        var bytes = TeamReportPdf.Render(report);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void TeamReportPdf_ScoredJudgedTeam_WithNoPositionYet_ProducesAPdf()
    {
        var report = JudgedTeamReport() with { ClassificationPosition = null };

        var bytes = TeamReportPdf.Render(report);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void TeamReportPdf_WithLogo_ProducesAPdf()
    {
        var bytes = TeamReportPdf.Render(TeamSportReport(logoBytes: TinyLogo));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void TeamReportPdf_WithACompetitionColour_ProducesAPdf()
    {
        var bytes = TeamReportPdf.Render(TeamSportReport(accentColor: "#7B1FA2"));

        bytes.Should().NotBeEmpty();
    }

    private static AthleteReport AthleteWithOneEntry(
        bool judged = false, byte[]? logoBytes = null, byte[]? photoBytes = null, string? accentColor = null) => new(
        Guid.NewGuid(), "Juan", "Pérez", "12345678", new DateOnly(2000, 1, 1), "M", 68.5m,
        photoBytes ?? TinyLogo, PhotoKey: null,
        judged
            ? [new AthleteEntryReport(
                Guid.NewGuid(), Guid.NewGuid(), "Poomsae Individual", Guid.NewGuid(), "Poomsae Sub-15",
                Guid.NewGuid(), "Copa Poomsae", "Taekwondo (Poomsae)", IsJudged: true, Withdrawn: false,
                [], [], 765, PerformanceStatus.Scored, 2, logoBytes, accentColor)]
            : [new AthleteEntryReport(
                Guid.NewGuid(), Guid.NewGuid(), "Equipo A", Guid.NewGuid(), "Primera",
                Guid.NewGuid(), "Copa Apertura", "Fútbol", IsJudged: false, Withdrawn: false,
                [new AthleteMatchLine(
                    Guid.NewGuid(), new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
                    "Equipo B", true, 2, 1, MatchState.Finished, "Ganado")],
                [new AthleteMetricTotal("Gol", 2)],
                null, null, null, logoBytes, accentColor)]);

    [Fact]
    public void AthleteReportPdf_TeamSportEntry_ProducesAPdf()
    {
        var bytes = AthleteReportPdf.Render(AthleteWithOneEntry());

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void AthleteReportPdf_JudgedEntry_ProducesAPdf()
    {
        var bytes = AthleteReportPdf.Render(AthleteWithOneEntry(judged: true));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void AthleteReportPdf_NoRealPhoto_DrawsThePlaceholderInstead_ProducesAPdf()
    {
        // El mismo silhouette que ya usa el sistema de credenciales cuando
        // nadie subio una foto — AthletePhoto.BytesAsync es quien realmente
        // decide devolverlo, esto solo confirma que el renderer lo dibuja
        // sin problema.
        var bytes = AthleteReportPdf.Render(AthleteWithOneEntry(photoBytes: SportFrog.Api.Infrastructure.Storage.DefaultAvatar.Bytes));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void AthleteReportPdf_NoRegistrationsAtAll_ProducesAPdf()
    {
        var report = new AthleteReport(
            Guid.NewGuid(), "Juan", "Pérez", "12345678", new DateOnly(2000, 1, 1), null, null,
            TinyLogo, PhotoKey: null, []);

        var bytes = AthleteReportPdf.Render(report);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void AthleteReportPdf_WithdrawnEntry_NoMetricsOrMatchesYet_ProducesAPdf()
    {
        var entry = new AthleteEntryReport(
            Guid.NewGuid(), Guid.NewGuid(), "Equipo A", Guid.NewGuid(), "Primera",
            Guid.NewGuid(), "Copa Apertura", "Fútbol", IsJudged: false, Withdrawn: true,
            [], [], null, null, null, LogoBytes: null, AccentColor: null);

        var bytes = AthleteReportPdf.Render(new AthleteReport(
            Guid.NewGuid(), "Juan", "Pérez", "12345678", new DateOnly(2000, 1, 1), null, null,
            TinyLogo, PhotoKey: null, [entry]));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void AthleteReportPdf_WithLogo_ProducesAPdf()
    {
        var bytes = AthleteReportPdf.Render(AthleteWithOneEntry(logoBytes: TinyLogo));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void AthleteReportPdf_WithACompetitionColour_ProducesAPdf()
    {
        var bytes = AthleteReportPdf.Render(AthleteWithOneEntry(accentColor: "#7B1FA2"));

        bytes.Should().NotBeEmpty();
    }
}
