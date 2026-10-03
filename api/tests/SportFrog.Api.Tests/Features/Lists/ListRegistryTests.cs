using AwesomeAssertions;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// Built from every registered <see cref="IListProvider"/> the same way
/// <see cref="SportFrog.Api.Features.Draw.CalendarDrawRegistry"/> is built
/// from every registered <see cref="SportFrog.Api.Features.Draw.ICalendarDraw"/> —
/// these mirror that type's own tests, including the duplicate-slug case a
/// wiring mistake in Program.cs would otherwise hit only in production.
/// </summary>
public sealed class ListRegistryTests
{
    private sealed class StubListProvider(string slug) : IListProvider
    {
        public string Slug { get; } = slug;

        public string Label => Slug;

        public IReadOnlyList<ListParameter> Parameters => [];

        public Task<bool> AppliesToAsync(
            string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<ListTable?> LoadAsync(
            ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken) =>
            Task.FromResult<ListTable?>(null);
    }

    [Fact]
    public void For_ResolvesEachSlugToItsOwnProvider()
    {
        var goleadores = new StubListProvider("goleadores");
        var posiciones = new StubListProvider("posiciones");
        var registry = new ListRegistry([goleadores, posiciones]);

        registry.For("goleadores").Should().BeSameAs(goleadores);
        registry.For("posiciones").Should().BeSameAs(posiciones);
    }

    [Fact]
    public void For_UnregisteredSlug_ThrowsRatherThanReturningNull()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores")]);

        var attempt = () => registry.For("tarjetas");

        attempt.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_DuplicateSlug_ThrowsRatherThanSilentlyKeepingOne()
    {
        var building = () => new ListRegistry(
            [new StubListProvider("goleadores"), new StubListProvider("goleadores")]);

        building.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryFor_RegisteredSlug_ReturnsTrueAndTheProvider()
    {
        var goleadores = new StubListProvider("goleadores");
        var registry = new ListRegistry([goleadores]);

        registry.TryFor("goleadores", out var provider).Should().BeTrue();
        provider.Should().BeSameAs(goleadores);
    }

    [Fact]
    public void TryFor_UnregisteredSlug_ReturnsFalseRatherThanThrowing()
    {
        var registry = new ListRegistry([new StubListProvider("goleadores")]);

        registry.TryFor("tarjetas", out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void All_ListsEveryRegisteredProvider()
    {
        var goleadores = new StubListProvider("goleadores");
        var posiciones = new StubListProvider("posiciones");
        var registry = new ListRegistry([goleadores, posiciones]);

        registry.All.Should().BeEquivalentTo([goleadores, posiciones]);
    }
}
