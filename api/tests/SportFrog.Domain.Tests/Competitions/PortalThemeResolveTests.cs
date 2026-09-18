using AwesomeAssertions;
using SportFrog.Domain.Competitions;

namespace SportFrog.Domain.Tests.Competitions;

/// <summary>
/// <see cref="PortalTheme.Resolve"/> is the one place the stored theme is
/// turned into what the public page renders from: it fills every default and
/// folds in the pre-theme <see cref="PublicSettings.AccentColor"/>. The
/// contract that matters is that a competition which set nothing stays on the
/// plain theme (null out), and one which set only the old accent colour keeps
/// exactly the look it had.
/// </summary>
public sealed class PortalThemeResolveTests
{
    [Fact]
    public void Resolve_ReturnsNull_WhenNothingWasCustomized()
    {
        PortalTheme.Resolve(null).Should().BeNull();
        PortalTheme.Resolve(new PublicSettings()).Should().BeNull();
        PortalTheme.Resolve(new PublicSettings { AccentColor = "   " }).Should().BeNull();
    }

    [Fact]
    public void Resolve_FoldsTheLegacyAccentColourIntoThePrimary_WhenNoThemeWasSet()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings { AccentColor = "#C81E1E" });

        resolved.Should().NotBeNull();
        resolved!.Primary.Should().Be("#C81E1E");
        // A page that only ever set the accent colour gets base-theme
        // defaults for everything else — same page, applied consistently.
        resolved.Secondary.Should().Be("#C81E1E");
        resolved.HeadingFont.Should().Be("inter");
        resolved.Corners.Should().Be("soft");
        resolved.HeroStyle.Should().Be("solid");
        resolved.Density.Should().Be("normal");
        resolved.Decoration.Should().Be("none");
        resolved.ContentFigure.Should().Be("none");
        resolved.ShowLogoBackground.Should().BeTrue();
        resolved.HeroLayout.Should().Be("standard");
        resolved.HeroVariant.Should().Be("standard");
        resolved.StandingsVariant.Should().Be("standard");
        resolved.MatchCardVariant.Should().Be("standard");
        resolved.BracketVariant.Should().Be("standard");
        resolved.PrimaryContrast.Should().BeNull();
        resolved.Surface.Should().BeNull();
        resolved.HeroGradientTo.Should().BeNull();
        resolved.ContentFigureColor.Should().BeNull();
    }

    [Fact]
    public void Resolve_PrefersTheThemePrimaryOverTheLegacyAccentColour()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            AccentColor = "#111111",
            Theme = new PortalTheme { Primary = "#2E5AAC" },
        });

        resolved!.Primary.Should().Be("#2E5AAC");
        resolved.Secondary.Should().Be("#2E5AAC", "an unset secondary tracks the primary");
    }

    [Fact]
    public void Resolve_KeepsEveryChoiceThatWasMade()
    {
        var theme = new PortalTheme
        {
            Primary = "#0F766E",
            PrimaryContrast = "#FFFFFF",
            Secondary = "#F59E0B",
            Surface = "#FAFAF9",
            HeadingFont = "oswald",
            Corners = "sharp",
            HeroStyle = "gradient",
            HeroGradientTo = "#134E4A",
            // El único bool acá -- false, no el default, para que este test
            // ("se conserva cada elección") de verdad distinga "se guardó
            // false" de "nunca se tocó y cayó en true".
            ShowLogoBackground = false,
            FocusX = 30,
            FocusY = 70,
            Density = "compact",
            Decoration = "bold",
            ContentFigure = "wave",
            ContentFigureColor = "#0EA5E9",
            HeroLayout = "centered",
            HeroVariant = "editorial",
            StandingsVariant = "cards",
            MatchCardVariant = "matchup",
            BracketVariant = "detailed",
        };

        var resolved = PortalTheme.Resolve(new PublicSettings { Theme = theme });

        resolved.Should().BeEquivalentTo(new ResolvedPortalTheme(
            Primary: "#0F766E",
            PrimaryContrast: "#FFFFFF",
            Secondary: "#F59E0B",
            Surface: "#FAFAF9",
            HeadingFont: "oswald",
            Corners: "sharp",
            HeroStyle: "gradient",
            HeroGradientTo: "#134E4A",
            ShowLogoBackground: false,
            FocusX: 30,
            FocusY: 70,
            Density: "compact",
            Decoration: "bold",
            ContentFigure: "wave",
            ContentFigureColor: "#0EA5E9",
            HeroLayout: "centered",
            HeroVariant: "editorial",
            StandingsVariant: "cards",
            MatchCardVariant: "matchup",
            BracketVariant: "detailed"));
    }

    [Fact]
    public void Resolve_DefaultsTheFocalPointToCentre_WhenNeitherAxisWasSet()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings { Theme = new PortalTheme { Primary = "#000000" } });

        resolved!.FocusX.Should().Be(50);
        resolved.FocusY.Should().Be(50);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Resolve_FallsBackToCentre_WhenAnAxisIsOutOfRange(double outOfRange)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", FocusX = outOfRange },
        });

        resolved!.FocusX.Should().Be(50);
    }

    [Theory]
    [InlineData("comic-sans")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenAChoiceFieldIsUnknownOrBlank(string? badFont)
    {
        // The contract validator already refuses an unknown value, but Resolve
        // must not hand the page a font it cannot load if one slips through
        // (an older row, a direct DB edit).
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", HeadingFont = badFont },
        });

        resolved!.HeadingFont.Should().Be("inter");
    }

    [Theory]
    [InlineData("cramped")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenDensityIsUnknownOrBlank(string? badDensity)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", Density = badDensity },
        });

        resolved!.Density.Should().Be("normal");
    }

    [Theory]
    [InlineData("glitter")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenDecorationIsUnknownOrBlank(string? badDecoration)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", Decoration = badDecoration },
        });

        resolved!.Decoration.Should().Be("none");
    }

    [Theory]
    [InlineData("confetti")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenContentFigureIsUnknownOrBlank(string? badFigure)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", ContentFigure = badFigure },
        });

        resolved!.ContentFigure.Should().Be("none");
    }

    [Theory]
    [InlineData("diagonal")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenHeroLayoutIsUnknownOrBlank(string? badLayout)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", HeroLayout = badLayout },
        });

        resolved!.HeroLayout.Should().Be("standard");
    }

    [Theory]
    [InlineData("broadcast")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenHeroVariantIsUnknownOrBlank(string? badVariant)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", HeroVariant = badVariant },
        });

        resolved!.HeroVariant.Should().Be("standard");
    }

    [Theory]
    [InlineData("spreadsheet")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenStandingsVariantIsUnknownOrBlank(string? badVariant)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", StandingsVariant = badVariant },
        });

        resolved!.StandingsVariant.Should().Be("standard");
    }

    [Theory]
    [InlineData("wide")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenMatchCardVariantIsUnknownOrBlank(string? badVariant)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", MatchCardVariant = badVariant },
        });

        resolved!.MatchCardVariant.Should().Be("standard");
    }

    [Theory]
    [InlineData("tree")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_FallsBackToTheDefault_WhenBracketVariantIsUnknownOrBlank(string? badVariant)
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "#000000", BracketVariant = badVariant },
        });

        resolved!.BracketVariant.Should().Be("standard");
    }

    [Fact]
    public void Resolve_DefaultsTheHeroStyleToImage_WhenABannerWasSetBeforeHeroStylesExisted()
    {
        // A competition that uploaded a banner with the old editor never chose
        // a hero style. Defaulting it to "solid" would drop its cover; it
        // defaults to "image" so the page keeps what it had.
        var withBanner = PortalTheme.Resolve(new PublicSettings
        {
            BannerKey = "org/competition-banners/abc.webp",
            AccentColor = "#333333",
        });
        withBanner!.HeroStyle.Should().Be("image");

        var withoutBanner = PortalTheme.Resolve(new PublicSettings { AccentColor = "#333333" });
        withoutBanner!.HeroStyle.Should().Be("solid");
    }

    [Fact]
    public void Resolve_KeepsAnExplicitHeroStyle_EvenWithABanner()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            BannerKey = "org/competition-banners/abc.webp",
            Theme = new PortalTheme { Primary = "#000000", HeroStyle = "gradient" },
        });

        resolved!.HeroStyle.Should().Be("gradient");
    }

    [Fact]
    public void Resolve_FallsBackToTheBrandGreen_WhenAThemeWasOpenedButNoPrimaryChosen()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { HeadingFont = "anton" },
        });

        resolved!.Primary.Should().Be("#1B8A2E");
        resolved.HeadingFont.Should().Be("anton");
    }

    [Fact]
    public void Resolve_TrimsSurroundingWhitespaceFromColours()
    {
        var resolved = PortalTheme.Resolve(new PublicSettings
        {
            Theme = new PortalTheme { Primary = "  #ABCDEF  " },
        });

        resolved!.Primary.Should().Be("#ABCDEF");
    }
}
