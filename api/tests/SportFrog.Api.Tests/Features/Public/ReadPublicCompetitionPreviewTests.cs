using AwesomeAssertions;
using SportFrog.Api.Features.Public;

namespace SportFrog.Api.Tests.Features.Public;

/// <summary>
/// <see cref="ReadPublicCompetitionPreview.Render"/> writes raw HTML from
/// strings a competitor or an organizer chose, not from anything this
/// codebase controls — the one thing that matters here is that neither one
/// can break out of a meta tag's attribute and inject markup of its own.
/// </summary>
public sealed class ReadPublicCompetitionPreviewTests
{
    private static ReadPublicCompetitionPreview.PreviewData Data(
        string title = "Copa Apertura · Liga de Prueba",
        string description = "Una competencia de prueba.",
        string? imageUrl = null) =>
        new(title, description, imageUrl, "liga-de-prueba", "copa-apertura");

    [Fact]
    public void Render_EscapesAQuoteInTheTitle_SoItCannotBreakOutOfTheAttribute()
    {
        var html = ReadPublicCompetitionPreview.Render(
            Data(title: "\"><script>alert(1)</script>"), "https://sportfrog.example/public/liga-de-prueba/copa-apertura");

        html.Should().NotContain("<script>");
        html.Should().Contain("&quot;&gt;&lt;script&gt;");
    }

    [Fact]
    public void Render_EscapesAnAmpersandInTheDescription()
    {
        var html = ReadPublicCompetitionPreview.Render(
            Data(description: "Copa & Liga Deportiva"), "https://sportfrog.example/public/liga-de-prueba/copa-apertura");

        html.Should().Contain("Copa &amp; Liga Deportiva");
        html.Should().NotContain("Copa & Liga");
    }

    [Fact]
    public void Render_WithNoImage_OmitsTheImageTags_AndUsesTheSummaryCard()
    {
        var html = ReadPublicCompetitionPreview.Render(Data(imageUrl: null), "https://sportfrog.example/public/liga-de-prueba/copa-apertura");

        html.Should().NotContain("og:image");
        html.Should().NotContain("twitter:image");
        // Exactamente "summary", no "summary_large_image" -- de ahi la
        // comilla de cierre pegada, para que el segundo caso no matchee acá.
        html.Should().Contain("""twitter:card" content="summary">""");
    }

    [Fact]
    public void Render_WithAnImage_IncludesItAndUsesTheLargeImageCard()
    {
        var html = ReadPublicCompetitionPreview.Render(
            Data(imageUrl: "https://storage.example/banner.jpg"), "https://sportfrog.example/public/liga-de-prueba/copa-apertura");

        html.Should().Contain("""og:image" content="https://storage.example/banner.jpg""");
        html.Should().Contain("""twitter:card" content="summary_large_image">""");
    }

    [Fact]
    public void Render_IncludesTheAppUrl_InBothOpenGraphAndTheRefresh()
    {
        var appUrl = "https://sportfrog.example/public/liga-de-prueba/copa-apertura";

        var html = ReadPublicCompetitionPreview.Render(Data(), appUrl);

        html.Should().Contain($"""og:url" content="{appUrl}""");
        html.Should().Contain($"""content="0; url={appUrl}""");
    }

    [Theory]
    [InlineData("Corto", "Corto")]
    public void Truncate_ShortText_IsUnchanged(string value, string expected) =>
        ReadPublicCompetitionPreview.Truncate(value, 200).Should().Be(expected);

    [Fact]
    public void Truncate_LongText_CutsToTheLimitAndMarksItWasCut()
    {
        var value = new string('a', 250);

        var truncated = ReadPublicCompetitionPreview.Truncate(value, 200);

        truncated.Should().HaveLength(201); // 200 + el "…"
        truncated.Should().EndWith("…");
    }
}
