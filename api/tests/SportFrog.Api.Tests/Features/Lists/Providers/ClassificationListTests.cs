using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists.Providers;

/// <summary>Whether <see cref="ClassificationList"/> actually reads a judged category's classification stage — a
/// query that compiles can still fail the moment EF Core tries to translate it, which only shows up against a
/// real database.</summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ClassificationListTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Fixture(Guid OrgId, Guid CompetitionId, Guid CategoryId);

    private sealed class Session(SportFrogDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }

    private async Task<Fixture> SeedAsync(string sportCode)
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
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

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club A" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId, OrgId = orgId, SportCode = sportCode, Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 1, Label = "ronda", Minutes = 1 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId, OrgId = orgId, SportCode = sportCode, RulesetId = rulesetId,
            Name = "Competencia", Slug = $"comp-{competitionId:N}", Season = "2026",
            Format = CompetitionFormat.League, Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Poomsae Sub-15" });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();
        await setup.DisposeAsync();

        return new Fixture(orgId, competitionId, categoryId);
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
        var fx = await SeedAsync("taekwondo_poomsae");
        await using var session = await OpenAsync(fx);

        var table = await new ClassificationList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = Guid.NewGuid().ToString() }),
            session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_NotAJudgedCategory_ReturnsNull()
    {
        var fx = await SeedAsync("football");
        await using var session = await OpenAsync(fx);

        var table = await new ClassificationList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context, CancellationToken.None);

        table.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_JudgedCategory_RanksByScoreAndTiesSharePlace()
    {
        var fx = await SeedAsync("taekwondo_poomsae");
        await using var session = await OpenAsync(fx);

        await using (var write = fixture.CreateAppContext())
        {
            var transaction = await write.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, fx.OrgId);

            var teamA = new Team { Id = Guid.NewGuid(), OrgId = fx.OrgId, ClubId = Guid.NewGuid(), CategoryId = fx.CategoryId, Name = "Atleta A", IsIndividual = true };
            var teamB = new Team { Id = Guid.NewGuid(), OrgId = fx.OrgId, ClubId = Guid.NewGuid(), CategoryId = fx.CategoryId, Name = "Atleta B", IsIndividual = true };
            var teamC = new Team { Id = Guid.NewGuid(), OrgId = fx.OrgId, ClubId = Guid.NewGuid(), CategoryId = fx.CategoryId, Name = "Atleta C", IsIndividual = true };

            // Named apart from SeedAsync's own "Club A": club names are unique per organization.
            write.Clubs.Add(new Club { Id = teamA.ClubId, OrgId = fx.OrgId, Name = "Club X" });
            write.Clubs.Add(new Club { Id = teamB.ClubId, OrgId = fx.OrgId, Name = "Club Y" });
            write.Clubs.Add(new Club { Id = teamC.ClubId, OrgId = fx.OrgId, Name = "Club Z" });
            write.Teams.AddRange(teamA, teamB, teamC);
            await write.SaveChangesAsync();

            write.Performances.Add(new Performance
            {
                Id = Guid.NewGuid(), OrgId = fx.OrgId, CompetitionId = fx.CompetitionId, CategoryId = fx.CategoryId,
                TeamId = teamA.Id, Score = 85,
            });
            write.Performances.Add(new Performance
            {
                Id = Guid.NewGuid(), OrgId = fx.OrgId, CompetitionId = fx.CompetitionId, CategoryId = fx.CategoryId,
                TeamId = teamB.Id, Score = 85,
            });
            write.Performances.Add(new Performance
            {
                Id = Guid.NewGuid(), OrgId = fx.OrgId, CompetitionId = fx.CompetitionId, CategoryId = fx.CategoryId,
                TeamId = teamC.Id, Score = null,
            });

            await write.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var table = await new ClassificationList().LoadAsync(
            new ListScope(new Dictionary<string, string> { ["categoryId"] = fx.CategoryId.ToString() }),
            session.Context, CancellationToken.None);

        table!.Title.Should().Be("Clasificación — Poomsae Sub-15");

        var rows = table.Sections.Single().Rows;
        rows.Should().HaveCount(3);

        // Atleta A and Atleta B tie for first; the third, unscored, trails with no position.
        rows[0][0].Should().Be("1");
        rows[1][0].Should().Be("1");
        rows[2][0].Should().Be("-");
        rows[2][2].Should().BeNull();
    }
}
