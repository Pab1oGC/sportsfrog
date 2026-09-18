using AwesomeAssertions;
using SportFrog.Api.Features.Matches;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// QuestPDF's own layout checks only run inside <c>GeneratePdf()</c> — see
/// <c>CompetitionBulletinPdfTests</c>'s own remarks for why this is the only
/// thing that would catch a composition mistake before an organizer does.
/// </summary>
public sealed class FixturePdfTests
{
    private static CompetitionBranding Branding(byte[]? logo = null, string? color = null) => new(logo, color);

    private static ReadMatches.Summary Match(
        string category = "Sub-15",
        string home = "Equipo A",
        string away = "Equipo B",
        DateTimeOffset? at = null,
        int? homeTotal = null,
        int? awayTotal = null,
        MatchState status = MatchState.Scheduled,
        string? venue = "Sede Central",
        string? space = "Cancha 1") =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), category,
            Guid.NewGuid(), home, null, Guid.NewGuid(), away, null,
            venue is null ? null : Guid.NewGuid(), venue, space,
            at, 1, null, status, null, null, homeTotal, awayTotal, null, null, null, null, null);

    [Fact]
    public void Render_WholeCompetition_ShowsCategoryColumn_ProducesAPdf()
    {
        var matches = new[]
        {
            Match(category: "Sub-15", at: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
            Match(category: "Primera", at: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
                homeTotal: 2, awayTotal: 1, status: MatchState.Finished),
        };

        var bytes = FixturePdf.Render("Copa Apertura", categoryName: null, round: null, branding: Branding(), matches: matches);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_OneCategory_OmitsCategoryColumn_ProducesAPdf()
    {
        var matches = new[] { Match() };

        var bytes = FixturePdf.Render("Copa Apertura", "Sub-15", null, Branding(), matches);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_NoMatchesYet_ProducesAPdf()
    {
        var bytes = FixturePdf.Render("Copa Apertura", null, null, Branding(), []);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_UndatedAndNoVenueMatches_ProducesAPdf()
    {
        var matches = new[] { Match(at: null, venue: null, space: null) };

        var bytes = FixturePdf.Render("Copa Apertura", null, null, Branding(), matches);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_EveryMatchState_ProducesAPdf()
    {
        var matches = Enum.GetValues<MatchState>()
            .Select(status => Match(status: status))
            .ToList();

        var bytes = FixturePdf.Render("Copa Apertura", null, null, Branding(), matches);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_ManyMatches_StillProducesAPdf()
    {
        var matches = Enumerable.Range(1, 300)
            .Select(i => Match(
                home: $"Equipo {i}", away: $"Equipo {i + 1}",
                at: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero).AddHours(i)))
            .ToList();

        var bytes = FixturePdf.Render("Copa Apertura", null, null, Branding(), matches);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_WithLogo_ProducesAPdf()
    {
        // PNG de 1x1, minimo -- alcanza para que QuestPDF lo decodifique de
        // verdad al generar.
        var logo = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        var bytes = FixturePdf.Render("Copa Apertura", null, null, Branding(logo), [Match()]);

        bytes.Should().NotBeEmpty();
    }

    // ---- Round / jornada label -------------------------------------------

    [Fact]
    public void Render_WholeCompetitionOneRound_ProducesAPdf()
    {
        var bytes = FixturePdf.Render("Copa Apertura", categoryName: null, round: 3, branding: Branding(), matches: [Match()]);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Render_OneCategoryOneRound_ProducesAPdf()
    {
        var bytes = FixturePdf.Render("Copa Apertura", "Sub-15", round: 3, branding: Branding(), matches: [Match()]);

        bytes.Should().NotBeEmpty();
    }
}
