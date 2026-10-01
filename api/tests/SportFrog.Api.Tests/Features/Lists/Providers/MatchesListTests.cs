using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;
using SportFrog.Domain.Scheduling;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="MatchesList"/> actually reads a category's calendar — a query that compiles can
/// still fail the moment EF Core tries to translate it, which only shows up against a real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class MatchesListTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid CategoryId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>One finished match with a venue, one undated knockout slot still waiting on a semifinal —
    /// enough to pin ordering, score formatting, the venue fallback, and the "Ganador de" placeholder.</summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var semifinalId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubAId, OrgId = orgId, Name = "Club A" });
        setup.Clubs.Add(new Club { Id = clubBId, OrgId = orgId, Name = "Club B" });
        setup.Venues.Add(new Venue { Id = venueId, OrgId = orgId, Name = "Sede Central" });
        setup.VenueSpaces.Add(new VenueSpace { Id = spaceId, OrgId = orgId, VenueId = venueId, Name = "Cancha 1" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = "football", Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = "football", RulesetId = rulesetId,
            Name = "Copa de Prueba", Slug = $"copa-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.Knockout, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = categoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = categoryId, Name = "Equipo B" });
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            Id = semifinalId, OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeTeamId = teamAId, AwayTeamId = teamBId, HomeTotal = 2, AwayTotal = 1, Status = MatchState.Finished,
            RecordedBy = userId, VenueSpaceId = spaceId, Phase = "semifinal", RoundNumber = 1,
            ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });
        await setup.SaveChangesAsync();

        setup.Matches.Add(new Match
        {
            // Home is still "whoever wins the semifinal"; Away is already a
            // real team (a bye, say) -- one placeholder side and one named
            // side on the same row is exactly what the provider's fallback
            // chain has to tell apart.
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = competitionId, CategoryId = categoryId,
            HomeSourceMatchId = semifinalId, AwayTeamId = teamBId, Status = MatchState.Scheduled,
            Phase = Bracket.FinalPhase, RoundNumber = 2, ScheduledAt = null,
        });
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        return new Fixture(orgId, categoryId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    [Fact]
    public async Task LoadAsync_UnknownCategory_ReturnsNull()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new MatchesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = Guid.NewGuid().ToString() }),
            session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_KnownCategory_TitleNamesTheCategoryAndListsTheMatchesUndatedFirst()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new MatchesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Title.Should().Be("Partidos — Primera");

        var rows = table.Sections.Single().Rows;
        rows.Should().HaveCount(2);
        rows[1][0].Should().BeNull(); // the undated final sorts last, same as ReadMatches.Ordered
    }

    [Fact]
    public async Task LoadAsync_FinishedMatch_ShowsTheScoreAndVenue()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new MatchesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context, CancellationToken.None);

        var finished = table!.Sections.Single().Rows[0];

        finished[1].Should().Be("Equipo A");
        finished[2].Should().Be("2 - 1");
        finished[3].Should().Be("Equipo B");
        finished[4].Should().Be("Sede Central — Cancha 1");
        finished[5].Should().Be("Finalizado");
    }

    [Fact]
    public async Task LoadAsync_UndecidedKnockoutSlot_NamesTheSourcesPhaseRatherThanATeam()
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var table = await new MatchesList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context, CancellationToken.None);

        var pending = table!.Sections.Single().Rows[1];

        pending[1].Should().Be("Ganador de semifinal");
        pending[2].Should().Be("vs");
        pending[3].Should().Be("Equipo B");
        pending[4].Should().Be("Sin definir");
        pending[5].Should().Be("Programado");
    }
}
