using AwesomeAssertions;
using SportFrog.Api.Features.Lists;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// <see cref="ListScope"/> hands a provider raw strings and nothing more —
/// these pin how it parses them into the types a provider actually asked
/// for, and that a value given but not shaped like the one asked for reads
/// as absent rather than throwing mid-request over one bad query parameter.
/// </summary>
public sealed class ListScopeTests
{
    private static ListScope ScopeOf(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    [Fact]
    public void GetString_ValueGiven_ReturnsIt()
    {
        var scope = ScopeOf(("search", "Diaz"));

        scope.GetString("search").Should().Be("Diaz");
    }

    [Fact]
    public void GetString_NameNotGiven_ReturnsNull()
    {
        ListScope.Empty.GetString("search").Should().BeNull();
    }

    [Fact]
    public void GetGuid_ValidGuid_ParsesIt()
    {
        var categoryId = Guid.NewGuid();
        var scope = ScopeOf(("categoryId", categoryId.ToString()));

        scope.GetGuid("categoryId").Should().Be(categoryId);
    }

    [Fact]
    public void GetGuid_NotAGuid_ReturnsNullRatherThanThrowing()
    {
        var scope = ScopeOf(("categoryId", "no-es-un-guid"));

        scope.GetGuid("categoryId").Should().BeNull();
    }

    [Fact]
    public void GetGuid_NameNotGiven_ReturnsNull()
    {
        ListScope.Empty.GetGuid("categoryId").Should().BeNull();
    }

    [Fact]
    public void GetInt_ValidInt_ParsesIt()
    {
        var scope = ScopeOf(("position", "2"));

        scope.GetInt("position").Should().Be(2);
    }

    [Fact]
    public void GetInt_NotAnInt_ReturnsNullRatherThanThrowing()
    {
        var scope = ScopeOf(("position", "segundo"));

        scope.GetInt("position").Should().BeNull();
    }

    [Fact]
    public void Empty_HasNoValues()
    {
        ListScope.Empty.GetString("anything").Should().BeNull();
    }
}
