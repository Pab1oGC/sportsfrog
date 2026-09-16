using AwesomeAssertions;
using SportFrog.Domain.Competitions;

namespace SportFrog.Domain.Tests.Competitions;

/// <summary>
/// <see cref="PortalSection.Resolve"/> turns whatever a competition stored —
/// short, out of order, or absent entirely — into the five sections the
/// public page actually renders, each named and in a definite order. The
/// contract that matters: nothing is ever lost, and a competition that
/// never touched this keeps the order it has always had.
/// </summary>
public sealed class PortalSectionResolveTests
{
    private static readonly string[] DefaultOrder = ["standings", "leaders", "classification", "calendar", "gallery"];

    [Fact]
    public void Resolve_ReturnsTheDefaultOrderAndLabels_WhenNothingWasStored()
    {
        var resolved = PortalSection.Resolve(null);

        resolved.Select(s => s.Key).Should().Equal(DefaultOrder);
        resolved.Select(s => s.Label).Should().Equal(
            "Tabla de posiciones", "Líderes", "Clasificación", "Calendario", "Fotos");
    }

    [Fact]
    public void Resolve_HonoursTheStoredOrder()
    {
        var resolved = PortalSection.Resolve([
            new PortalSection { Key = "calendar" },
            new PortalSection { Key = "standings" },
        ]);

        // El calendario y la tabla, en el orden guardado; lideres,
        // clasificacion y fotos, que la lista no nombro, al final en el
        // orden de siempre.
        resolved.Select(s => s.Key).Should().Equal("calendar", "standings", "leaders", "classification", "gallery");
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
        // las cinco secciones.
        var resolved = PortalSection.Resolve([new PortalSection { Key = "leaders" }]);

        resolved.Select(s => s.Key).Should().BeEquivalentTo(DefaultOrder);
        resolved.Should().HaveCount(5);
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

        resolved.Select(s => s.Key).Should().Equal("leaders", "standings", "classification", "calendar", "gallery");
        resolved.Last().Label.Should().Be("Fotos");
    }
}
