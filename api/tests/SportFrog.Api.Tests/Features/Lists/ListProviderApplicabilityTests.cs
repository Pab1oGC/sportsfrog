using AwesomeAssertions;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// Which sport each <see cref="SportFrog.Api.Features.Lists.IListProvider"/>
/// has anything to say for — the rule <c>ReadLists.Catalog</c> filters the
/// catalog by once a competition is chosen. Run against the real seeded
/// sports catalog (football, futsal, basketball, volleyball,
/// taekwondo_poomsae) rather than a hand-built one: <see cref="LeadersList"/>
/// and <see cref="CardsList"/> read the actual <c>sport_metrics</c> rows, so
/// a sport whose seed changes shape should fail this test, not pass it
/// silently.
/// </summary>
/// <remarks>
/// One <c>[Fact]</c> per provider rather than a <c>[Theory]</c> over
/// <c>IListProvider</c>/<c>ScoreMode</c> parameters: both are declared
/// <c>internal</c>, and xUnit requires this test class itself to be
/// <c>public</c> — a public method cannot carry an internal parameter type,
/// the same CS0051 this project has run into before. Declaring every case
/// inline sidesteps it instead of making either type public for a test's sake.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ListProviderApplicabilityTests(SportFrogDatabaseFixture fixture)
{
    // Sports and their metrics are a shared, organization-independent
    // catalog (see Sport's own remarks in the entity) — no transaction or
    // organization context is needed to read them, unlike every other
    // database-backed test in this feature.
    private SportFrogDbContext Database() => fixture.CreateAppContext();

    [Fact]
    public async Task AthletesList_AppliesToEverySport()
    {
        await using var database = Database();
        var provider = new AthletesList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task RosterList_AppliesToEverySport()
    {
        await using var database = Database();
        var provider = new RosterList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task RosterByPositionList_AppliesToEverySport()
    {
        await using var database = Database();
        var provider = new RosterByPositionList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task ClassificationList_OnlyAppliesToJudgedCompetitions()
    {
        await using var database = Database();
        var provider = new ClassificationList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeFalse();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeFalse();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task StandingsList_AppliesToEverySportExceptJudgedOnes()
    {
        await using var database = Database();
        var provider = new StandingsList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task MatchesList_AppliesToEverySportExceptJudgedOnes()
    {
        await using var database = Database();
        var provider = new MatchesList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task LeadersList_OnlyAppliesToASportWithAScoringMetric()
    {
        await using var database = Database();
        var provider = new LeadersList();

        // football: 'goal' affects the score.
        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        // basketball: 'free_throw', 'field_goal' and 'three_point' all affect the score.
        (await provider.AppliesToAsync("basketball", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        // volleyball: its only scoring-shaped metric ('point') is tallied by set, not summed — affects_score is false.
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeFalse();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task CardsList_OnlyAppliesToASportWithACardMetric()
    {
        await using var database = Database();
        var provider = new CardsList();

        (await provider.AppliesToAsync("football", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        (await provider.AppliesToAsync("futsal", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeTrue();
        // basketball has 'foul', not a card — never the same metric code.
        (await provider.AppliesToAsync("basketball", ScoreMode.Cumulative, database, CancellationToken.None)).Should().BeFalse();
        (await provider.AppliesToAsync("volleyball", ScoreMode.Sets, database, CancellationToken.None)).Should().BeFalse();
        (await provider.AppliesToAsync("taekwondo_poomsae", ScoreMode.Judged, database, CancellationToken.None)).Should().BeFalse();
    }
}
