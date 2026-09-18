using AwesomeAssertions;
using SportFrog.Domain.Competitions;

namespace SportFrog.Domain.Tests.Competitions;

/// <summary>
/// <see cref="PortalSection.Resolve"/> turns whatever a competition stored —
/// short, out of order, or absent entirely — into the six sections the
/// public page actually renders, each named and in a definite order. The
/// contract that matters: nothing is ever lost, and a competition that
/// never touched this keeps the order it has always had.
/// </summary>
public sealed class PortalSectionResolveTests
{
    private static readonly string[] DefaultOrder = ["standings", "leaders", "classification", "calendar", "gallery", "bracket"];

    [Fact]
    public void Resolve_ReturnsTheDefaultOrderAndLabels_WhenNothingWasStored()
    {
        var resolved = PortalSection.Resolve(null);

        resolved.Select(s => s.Key).Should().Equal(DefaultOrder);
        resolved.Select(s => s.Label).Should().Equal(
            "Tabla de posiciones", "Líderes", "Clasificación", "Calendario", "Fotos", "Llave");
    }

    [Fact]
    public void Resolve_HonoursTheStoredOrder()
    {
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "calendar" },
            new PortalSection { Key = "standings" },
        ]);

        // El calendario y la tabla, en el orden guardado; lideres,
        // clasificacion, fotos y llave, que la lista no nombro, al final en
        // el orden de siempre.
        resolved.Select(s => s.Key).Should().Equal("calendar", "standings", "leaders", "classification", "gallery", "bracket");
    }

    [Fact]
    public void Resolve_AppliesACustomLabel_ButFallsBackToTheDefaultWhenBlank()
    {
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "standings", Label = "Clasificación general" },
            new PortalSection { Key = "leaders", Label = "   " },
        ]);

        resolved.Single(s => s.Key == "standings").Label.Should().Be("Clasificación general");
        resolved.Single(s => s.Key == "leaders").Label.Should().Be("Líderes");
    }

    [Fact]
    public void Resolve_DropsAnUnknownKey_WithoutLosingAnyRealSection()
    {
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "sponsors" },
            new PortalSection { Key = "standings" },
        ]);

        resolved.Select(s => s.Key).Should().Equal(DefaultOrder);
    }

    [Fact]
    public void Resolve_KeepsOnlyTheFirstOccurrence_OfARepeatedKey()
    {
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "calendar", Label = "Fixture" },
            new PortalSection { Key = "calendar", Label = "Otro nombre" },
        ]);

        resolved.Should().ContainSingle(s => s.Key == "calendar")
            .Which.Label.Should().Be("Fixture");
    }

    [Fact]
    public void Resolve_NeverDropsASection_EvenFromAListShorterThanFive()
    {
        // Una lista de un solo elemento -- la que quedaria si alguien la
        // editara a mano hasta dejar solo una fila -- sigue resolviendo a
        // las seis secciones.
        var resolved = PortalSection.Resolve([new PortalSection { Key = "leaders" }]);

        resolved.Select(s => s.Key).Should().BeEquivalentTo(DefaultOrder);
        resolved.Should().HaveCount(6);
    }

    [Fact]
    public void Resolve_AppendsANewerSection_ForAListSavedBeforeItExisted()
    {
        // Simula exactamente el caso que la lista de arriba describe: una
        // competencia guardo su orden con las cuatro secciones originales,
        // antes de que "gallery" existiera.
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "leaders" },
            new PortalSection { Key = "standings" },
            new PortalSection { Key = "classification" },
            new PortalSection { Key = "calendar" },
        ]);

        resolved.Select(s => s.Key).Should().Equal("leaders", "standings", "classification", "calendar", "gallery", "bracket");
        resolved[4].Label.Should().Be("Fotos");
        resolved.Last().Label.Should().Be("Llave");
    }

    [Fact]
    public void Resolve_AppendsBracket_ForAListSavedBeforeItExisted()
    {
        // El mismo caso de arriba, un paso más adelante en el tiempo: una
        // competencia guardó su orden con las cinco secciones que existían
        // antes de que "bracket" existiera -- "gallery" incluida.
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "gallery" },
            new PortalSection { Key = "calendar" },
            new PortalSection { Key = "standings" },
            new PortalSection { Key = "leaders" },
            new PortalSection { Key = "classification" },
        ]);

        resolved.Select(s => s.Key).Should().Equal("gallery", "calendar", "standings", "leaders", "classification", "bracket");
        resolved.Last().Label.Should().Be("Llave");
    }
}
