using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Matches;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// Every <see cref="IListProvider"/> actually registered in <c>Program.cs</c>,
/// run against data it can genuinely resolve, and rendered through both
/// <see cref="ListXlsx"/> and <see cref="ListPdf"/>.
/// </summary>
/// <remarks>
/// Not a replacement for each provider's own tests — those pin what a
/// provider's rows and columns say. This pins something those cannot: that
/// the shape a real provider actually produces renders cleanly in both
/// formats at once, so a column whose declared <see cref="ListValueKind"/>
/// does not match the runtime type of its own values (a bug no single
/// renderer's own tests, built from hand-picked tables, would ever
/// manufacture) fails here instead of in whatever a PM happens to download
/// first.
///
/// New in <see cref="Slugs"/> is new in this test — add a provider to
/// <c>Program.cs</c> and to <see cref="ResolveCase"/> in the same change,
/// and it is covered the same way every other one already is.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ListProvidersParityTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid TeamCategoryId, Guid TeamId, Guid JudgedCategoryId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>
    /// One decided football category (a team with a numbered player, a
    /// finished match carrying a goal and a yellow card) and one judged
    /// category with a scored entry — between the two, every provider this
    /// feature registers has a scope it can resolve.
    /// </summary>
    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var judgedClubId = Guid.NewGuid();
        var footballCompetitionId = Guid.NewGuid();
        var footballRulesetId = Guid.NewGuid();
        var teamCategoryId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var rosterEntryId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var judgedCompetitionId = Guid.NewGuid();
        var judgedRulesetId = Guid.NewGuid();
        var judgedCategoryId = Guid.NewGuid();
        var judgedTeamId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        var goalMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == "football" && metric.Code == "goal")
            .Select(metric => metric.Id)
            .SingleAsync();
        var cardMetricId = await setup.SportMetrics
            .AsNoTracking()
            .Where(metric => metric.SportCode == "football" && metric.Code == "yellow_card")
            .Select(metric => metric.Id)
            .SingleAsync();

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
        setup.Clubs.Add(new Club { Id = judgedClubId, OrgId = orgId, Name = "Club C" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = footballRulesetId, OrgId = orgId, SportCode = "football", Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = footballCompetitionId, OrgId = orgId, SportCode = "football", RulesetId = footballRulesetId,
            Name = "Copa de Prueba", Slug = $"copa-{footballCompetitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = teamCategoryId, OrgId = orgId, CompetitionId = footballCompetitionId, Name = "Primera" });
        setup.Teams.Add(new Team { Id = teamAId, OrgId = orgId, ClubId = clubAId, CategoryId = teamCategoryId, Name = "Equipo A" });
        setup.Teams.Add(new Team { Id = teamBId, OrgId = orgId, ClubId = clubBId, CategoryId = teamCategoryId, Name = "Equipo B" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId, OrgId = orgId, FirstName = "Juan", LastName = "Diaz",
            DocumentId = $"doc-{athleteId:N}", BirthDate = new DateOnly(2000, 1, 1),
        });

        setup.Rulesets.Add(new Ruleset
        {
            Id = judgedRulesetId, OrgId = orgId, SportCode = "taekwondo_poomsae", Name = "Reglamento Poomsae",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 1, Label = "ronda", Minutes = 1 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = judgedCompetitionId, OrgId = orgId, SportCode = "taekwondo_poomsae", RulesetId = judgedRulesetId,
            Name = "Competencia de Prueba", Slug = $"comp-{judgedCompetitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = judgedCategoryId, OrgId = orgId, CompetitionId = judgedCompetitionId, Name = "Poomsae Sub-15" });
        setup.Teams.Add(new Team { Id = judgedTeamId, OrgId = orgId, ClubId = judgedClubId, CategoryId = judgedCategoryId, Name = "Atleta C", IsIndividual = true });

        await setup.SaveChangesAsync();

        setup.RosterEntries.Add(new RosterEntry
        {
            Id = rosterEntryId, OrgId = orgId, TeamId = teamAId, AthleteId = athleteId, JerseyNumber = 9,
        });

        setup.Matches.Add(new Match
        {
            Id = matchId, OrgId = orgId, CompetitionId = footballCompetitionId, CategoryId = teamCategoryId,
            HomeTeamId = teamAId, AwayTeamId = teamBId, HomeTotal = 2, AwayTotal = 1, Status = MatchState.Finished,
            RecordedBy = userId, ScheduledAt = new DateTimeOffset(2026, 5, 1, 15, 0, 0, TimeSpan.Zero),
        });

        setup.Performances.Add(new Performance
        {
            Id = Guid.NewGuid(), OrgId = orgId, CompetitionId = judgedCompetitionId, CategoryId = judgedCategoryId,
            TeamId = judgedTeamId, Score = 85,
        });

        await setup.SaveChangesAsync();

        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = orgId, MatchId = matchId, RosterEntryId = rosterEntryId,
            MetricId = goalMetricId, Quantity = 2,
        });
        setup.PlayerEvents.Add(new PlayerEvent
        {
            Id = Guid.NewGuid(), OrgId = orgId, MatchId = matchId, RosterEntryId = rosterEntryId,
            MetricId = cardMetricId, Quantity = 1,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        return new Fixture(orgId, teamCategoryId, teamAId, judgedCategoryId);
    }

    private async Task<Session> OpenAsync(Fixture fx)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        return new Session(context, transaction);
    }

    /// <summary>
    /// Every provider <c>Program.cs</c> registers, paired with a scope drawn
    /// from <see cref="SeedAsync"/> that it can actually resolve. Add a
    /// provider here when it is added there.
    /// </summary>
    public static IEnumerable<object[]> Slugs() =>
        new[]
        {
            "posiciones", "goleadores", "tarjetas", "plantel", "plantel-puesto",
            "deportistas", "partidos", "clasificacion",
        }
        .Select(slug => new object[] { slug });

    /// <summary>
    /// The provider named by <paramref name="slug"/>, and a scope drawn from
    /// <paramref name="fx"/> it can actually resolve. Kept out of
    /// <see cref="Slugs"/> itself — xUnit's own analyzer requires a public
    /// test class, and <see cref="IListProvider"/> is deliberately not
    /// public, so the pairing is resolved here instead of carried through
    /// <c>[MemberData]</c> as a second and third column.
    /// </summary>
    private static (IListProvider Provider, ListScope Scope) ResolveCase(string slug, Fixture fx) => slug switch
    {
        "posiciones" => (new StandingsList(), CategoryScope(fx.TeamCategoryId)),
        "goleadores" => (new LeadersList(), CategoryScope(fx.TeamCategoryId)),
        "tarjetas" => (new CardsList(), CategoryScope(fx.TeamCategoryId)),
        "plantel" => (new RosterList(), new ListScope(new Dictionary<string, string> { ["teamId"] = fx.TeamId.ToString() })),
        "plantel-puesto" => (new RosterByPositionList(), new ListScope(new Dictionary<string, string>
        {
            ["categoryId"] = fx.TeamCategoryId.ToString(), ["position"] = "1",
        })),
        "deportistas" => (new AthletesList(), ListScope.Empty),
        "partidos" => (new MatchesList(), CategoryScope(fx.TeamCategoryId)),
        "clasificacion" => (new ClassificationList(), CategoryScope(fx.JudgedCategoryId)),
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "No case wired up for this slug."),
    };

    private static ListScope CategoryScope(Guid categoryId) =>
        new(new Dictionary<string, string> { ["categoryId"] = categoryId.ToString() });

    [Theory]
    [MemberData(nameof(Slugs))]
    public async Task LoadAsync_ResolvesAndRendersInBothFormats(string slug)
    {
        var fx = await SeedAsync();
        await using var session = await OpenAsync(fx);

        var (provider, scope) = ResolveCase(slug, fx);
        provider.Slug.Should().Be(slug, "ResolveCase should pair each slug with that same provider");

        var table = await provider.LoadAsync(scope, session.Context, CancellationToken.None);

        table.Should().NotBeNull($"'{slug}' should resolve against the scope seeded for it");

        ListXlsx.Render(table!).Should().NotBeEmpty($"'{slug}' should render as Excel");

        var pdf = ListPdf.Render(table!);
        pdf.Should().NotBeEmpty($"'{slug}' should render as PDF");
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}
