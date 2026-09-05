using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Teams;

/// <summary>
/// The database-level guarantee behind "one team per club per category":
/// <c>uq_teams_club_category</c>. CreateTeam's own app-level pre-check reads
/// as an optimization on top of this — the constraint is what actually makes
/// the rule true under a race, and it is the piece a future individual-sport
/// team (taekwondo: one athlete, one delegation, several categories by
/// weight) will need relaxed. This test exists to prove exactly what that
/// change will be loosening.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TeamUniquenessTests(SportFrogDatabaseFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid OrgId, Guid ClubId, Guid CategoryAId, Guid CategoryBId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryAId = Guid.NewGuid();
        var categoryBId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club Unico" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "football",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "football",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category
        {
            Id = categoryAId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria A",
        });
        setup.Categories.Add(new Category
        {
            Id = categoryBId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria B",
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, clubId, categoryAId, categoryBId);
    }

    [Fact]
    public async Task SecondTeam_SameClub_SameCategory_ViolatesTheUniqueIndex()
    {
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryAId,
            Name = "Primer equipo",
        });
        await context.SaveChangesAsync();

        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryAId,
            Name = "Segundo equipo, mismo club, misma categoria",
        });

        var attempt = async () => await context.SaveChangesAsync();

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(attempt);
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("uq_teams_club_category", postgres.ConstraintName ?? postgres.MessageText);
    }

    [Fact]
    public async Task SecondTeam_SameClub_DifferentCategory_IsUnaffectedByTheIndex()
    {
        // The index is keyed on (category_id, club_id) together: a club
        // fielding a team in two different categories of the same
        // competition is the ordinary case, not a duplicate.
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryAId,
            Name = "Equipo en categoria A",
        });
        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryBId,
            Name = "Equipo en categoria B",
        });

        var rows = await context.SaveChangesAsync();

        Assert.Equal(2, rows);
    }

    [Fact]
    public async Task ReplacingASoftDeletedTeam_SameClub_SameCategory_IsAllowed()
    {
        // The index carries a `WHERE deleted_at IS NULL` filter: a club that
        // withdrew and re-entered the same category is not blocked by the
        // team it withdrew.
        var fx = await SeedAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var withdrawn = new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryAId,
            Name = "Equipo retirado",
        };
        context.Teams.Add(withdrawn);
        await context.SaveChangesAsync();

        withdrawn.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        context.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            ClubId = fx.ClubId,
            CategoryId = fx.CategoryAId,
            Name = "Equipo reinscrito",
        });

        var rows = await context.SaveChangesAsync();

        Assert.Equal(1, rows);
    }
}
