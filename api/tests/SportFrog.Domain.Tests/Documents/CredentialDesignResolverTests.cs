using AwesomeAssertions;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Documents;

namespace SportFrog.Domain.Tests.Documents;

/// <summary>
/// Which owner each value of a credential comes from, when a competition and
/// its design both have some and when one of them has none.
/// </summary>
public sealed class CredentialDesignResolverTests
{
    private static readonly PublicSettings Portal = new()
    {
        LogoKey = "org/a/competition-logo",
        BannerKey = "org/a/competition-banner",
    };

    private static readonly CredentialDesignValues Design = new(
        LegalText: "Texto de la organización.",
        BackgroundKey: "org/a/design-background",
        AccentColorHex: "#112233",
        LogoKey: "org/a/design-logo");

    [Fact]
    public void Competition_LogoWinsOverTheDesignsFallbackLogo()
    {
        var resolved = CredentialDesignResolver.Resolve(Portal, Design);

        resolved.LogoKey.Should().Be("org/a/competition-logo");
    }

    [Fact]
    public void Design_LogoIsUsedWhenTheCompetitionHasNone()
    {
        var resolved = CredentialDesignResolver.Resolve(new PublicSettings(), Design);

        resolved.LogoKey.Should().Be("org/a/design-logo");
    }

    [Fact]
    public void Design_BackgroundWinsOverTheCompetitionBanner()
    {
        var resolved = CredentialDesignResolver.Resolve(Portal, Design);

        resolved.BackgroundKey.Should().Be("org/a/design-background");
    }

    [Fact]
    public void CompetitionBanner_IsUsedAsBackgroundWhenTheDesignHasNone()
    {
        var withoutBackground = Design with { BackgroundKey = null };

        var resolved = CredentialDesignResolver.Resolve(Portal, withoutBackground);

        resolved.BackgroundKey.Should().Be("org/a/competition-banner");
    }

    [Fact]
    public void Design_AccentWinsOverThePortalAccent()
    {
        var portal = new PublicSettings { AccentColor = "#445566" };

        var resolved = CredentialDesignResolver.Resolve(portal, Design);

        resolved.AccentColorHex.Should().Be("#112233");
    }

    [Fact]
    public void PortalAccent_IsUsedWhenTheDesignHasNone()
    {
        var portal = new PublicSettings { AccentColor = "#445566" };
        var withoutAccent = Design with { AccentColorHex = null };

        var resolved = CredentialDesignResolver.Resolve(portal, withoutAccent);

        resolved.AccentColorHex.Should().Be("#445566");
    }

    [Fact]
    public void LegalText_IsTheDesignsTrimmed()
    {
        var resolved = CredentialDesignResolver.Resolve(null, Design with { LegalText = "  Aviso.  " });

        resolved.LegalText.Should().Be("Aviso.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LegalText_FallsBackToTheDecreeDefaultWhenBlank(string? blank)
    {
        var resolved = CredentialDesignResolver.Resolve(null, Design with { LegalText = blank });

        resolved.LegalText.Should().Be(CredentialDesignResolver.DefaultLegalText);
    }

    [Fact]
    public void NoDesignAtAll_PrintsWhatACredentialPrintedBeforeDesignsExisted()
    {
        var resolved = CredentialDesignResolver.Resolve(Portal, null);

        resolved.LegalText.Should().Be(CredentialDesignResolver.DefaultLegalText);
        resolved.LogoKey.Should().Be("org/a/competition-logo");
        resolved.BackgroundKey.Should().Be("org/a/competition-banner");
    }

    [Fact]
    public void NothingAnywhere_LeavesEveryOptionalValueEmpty()
    {
        var resolved = CredentialDesignResolver.Resolve(null, null);

        resolved.LogoKey.Should().BeNull();
        resolved.BackgroundKey.Should().BeNull();
        resolved.AccentColorHex.Should().BeNull();
    }
}
