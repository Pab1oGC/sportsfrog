using AwesomeAssertions;
using SportFrog.Domain.Documents;

namespace SportFrog.Domain.Tests.Documents;

/// <summary>
/// Whether white or dark text reads over a colour an operator chose for a
/// category or a zone — not fixed to white, because nothing stops that
/// choice from being a pale colour a white letter disappears into.
/// </summary>
public sealed class CredentialColorTests
{
    [Theory]
    [InlineData("#1F3864")] // the dark blue every seeded example uses
    [InlineData("#000000")]
    [InlineData("#6B2D90")] // the seeded "Cuerpo técnico" purple
    public void ForegroundFor_ADarkBackground_ReturnsWhite(string backgroundHex)
    {
        CredentialColor.ForegroundFor(backgroundHex).Should().Be("#FFFFFF");
    }

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#FFEB3B")] // a pale yellow an operator could reasonably pick
    [InlineData("#F0F0F0")]
    public void ForegroundFor_ALightBackground_ReturnsDark(string backgroundHex)
    {
        CredentialColor.ForegroundFor(backgroundHex).Should().Be("#1A1A1A");
    }

    [Fact]
    public void ForegroundFor_IsCaseInsensitiveToTheHexDigits()
    {
        CredentialColor.ForegroundFor("#1f3864").Should().Be(CredentialColor.ForegroundFor("#1F3864"));
    }
}
