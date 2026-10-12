using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Accreditation;

namespace SportFrog.Api.Tests.Features.Accreditation;

/// <summary>
/// The shape of the starting football catalogue, checked without a database:
/// what a new football competition is created with, and that nothing else is.
/// </summary>
public sealed class StartingCatalogTests
{
    private static Competition Competition() => new()
    {
        Id = Guid.NewGuid(),
        OrgId = Guid.NewGuid(),
        SportCode = StartingCatalog.FootballSportCode,
        RulesetId = Guid.NewGuid(),
        Name = "Copa",
        Slug = "copa",
        Season = "2026",
        Format = "league",
        Settings = new(),
    };

    [Fact]
    public void Build_ProducesTheDecreedFootballCatalogue()
    {
        var rows = StartingCatalog.Build(Competition(), DateTimeOffset.UtcNow);

        Assert.Equal(9, rows.Items.Count);
        Assert.Equal(2, rows.Categories.Count);
        Assert.Equal(18, rows.Links.Count);

        Assert.Equal(["Aa", "Ao"], rows.Categories.Select(category => category.Code));
    }

    [Fact]
    public void Build_GivesEachKindTheItemsTheGuideNamesForIt()
    {
        var rows = StartingCatalog.Build(Competition(), DateTimeOffset.UtcNow);

        var byKind = rows.Items.GroupBy(item => item.Kind).ToDictionary(group => group.Key, group => group.Select(item => item.Code).ToList());

        Assert.Equal(["FUT"], byKind[AccreditationItemKind.Discipline]);
        Assert.Equal(["VIL", "MPC", "EST"], byKind[AccreditationItemKind.Venue]);
        Assert.Equal(["TA", "COM"], byKind[AccreditationItemKind.Service]);
        Assert.Equal(["AZUL", "2", "R"], byKind[AccreditationItemKind.Zone]);
    }

    [Fact]
    public void Build_OnlyColoursTheZoneThatIsDrawnAsAColourBand()
    {
        var rows = StartingCatalog.Build(Competition(), DateTimeOffset.UtcNow);

        var coloured = rows.Items.Where(item => item.ColorHex is not null).Select(item => item.Code);

        Assert.Equal(["AZUL"], coloured);
    }

    [Fact]
    public void Build_FitsEveryCodeInItsBox()
    {
        var rows = StartingCatalog.Build(Competition(), DateTimeOffset.UtcNow);

        Assert.All(rows.Items, item => Assert.InRange(item.Code.Length, 1, 4));
        Assert.All(rows.Categories, category => Assert.InRange(category.Code.Length, 1, 4));
    }

    [Fact]
    public void Build_BelongsEntirelyToTheCompetitionItWasBuiltFor()
    {
        var competition = Competition();

        var rows = StartingCatalog.Build(competition, DateTimeOffset.UtcNow);

        Assert.All(rows.Items, item =>
        {
            Assert.Equal(competition.Id, item.CompetitionId);
            Assert.Equal(competition.OrgId, item.OrgId);
        });
        Assert.All(rows.Categories, category => Assert.Equal(competition.Id, category.CompetitionId));
    }

    [Fact]
    public void Build_LinksEveryCategoryToEveryItemOfItsOwnCompetition()
    {
        var rows = StartingCatalog.Build(Competition(), DateTimeOffset.UtcNow);

        var expected = rows.Categories
            .SelectMany(category => rows.Items.Select(item => (category.Id, item.Id)))
            .ToHashSet();
        var actual = rows.Links.Select(link => (link.CategoryId, link.ItemId)).ToHashSet();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("football", true)]
    [InlineData("volleyball", false)]
    [InlineData("futsal", false)]
    [InlineData("basketball", false)]
    public void HasFor_IsFootballOnly(string sportCode, bool expected) =>
        Assert.Equal(expected, StartingCatalog.HasFor(sportCode));
}
