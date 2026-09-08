using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Performances;

/// <summary>
/// GrantPublicReadOnPerformances, proven the same way the grant itself
/// works: as a real <c>sportfrog_public</c> connection, not as an assertion
/// about SQL text.
/// </summary>
/// <remarks>
/// AddPerformances granted the table to <c>sportfrog_app</c> only, because
/// nothing public read it yet. Two ways that migration could have been
/// wrong, and neither would have raised an error at write time: the grant
/// could still be missing (every read fails closed with a permission error,
/// which <see cref="ReadPublicClassification"/> has no code path to turn
/// into a 404 — a competition's public page would break outright) or the
/// grant could have come attached to a policy wide enough to leak another
/// organization's scores (which fails open, and is the one direction this
/// codebase treats as unforgivable — see the tenant-isolation remarks
/// throughout <c>api/README.md</c>). This asserts the actual boundary: a
/// row is visible with the right organization's context set, and invisible
/// with none.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PerformancesPublicReadTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Seeded(Guid OrgId, Guid PerformanceId);

    private async Task<Seeded> SeedPerformanceAsync()
    {
        var orgId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var performanceId = Guid.NewGuid();

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

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "taekwondo_poomsae",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 1, Label = "actuación", Minutes = 3 },
                Points = new Dictionary<string, int>(),
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "taekwondo_poomsae",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "knockout",
            IsPublic = true,
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Individual femenino",
        });

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club" });

        setup.Teams.Add(new Team
        {
            Id = teamId,
            OrgId = orgId,
            ClubId = clubId,
            CategoryId = categoryId,
            Name = "Deportista",
            IsIndividual = true,
        });

        setup.Performances.Add(new Performance
        {
            Id = performanceId,
            OrgId = orgId,
            CompetitionId = competitionId,
            CategoryId = categoryId,
            TeamId = teamId,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Seeded(orgId, performanceId);
    }

    /// <summary>
    /// Reads one row as <c>sportfrog_public</c>, exactly the way
    /// <c>PublicCompetitionReader</c> does: a connection of its own, the
    /// isolation context set with <c>SET LOCAL</c>, one query, gone at the
    /// end of the transaction.
    /// </summary>
    private async Task<int> CountAsPublicAsync(Guid? organizationContext, Guid performanceId)
    {
        await using var connection = new NpgsqlConnection(fixture.PublicConnectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        if (organizationContext is { } organizationId)
        {
            await using var setContext = new NpgsqlCommand(
                "SELECT set_config('app.current_org', $1, true)", connection, transaction);
            setContext.Parameters.Add(new NpgsqlParameter { Value = organizationId.ToString() });
            await setContext.ExecuteNonQueryAsync();
        }

        await using var query = new NpgsqlCommand(
            "SELECT count(*) FROM performances WHERE id = $1", connection, transaction);
        query.Parameters.Add(new NpgsqlParameter { Value = performanceId });

        var count = (long)(await query.ExecuteScalarAsync())!;

        await transaction.CommitAsync();

        return (int)count;
    }

    [Fact]
    public async Task SportfrogPublic_WithTheOwningOrganizationsContextSet_SeesTheRow()
    {
        var seeded = await SeedPerformanceAsync();

        var count = await CountAsPublicAsync(seeded.OrgId, seeded.PerformanceId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task SportfrogPublic_WithNoOrganizationContext_SeesNothing()
    {
        var seeded = await SeedPerformanceAsync();

        var count = await CountAsPublicAsync(organizationContext: null, seeded.PerformanceId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task SportfrogPublic_WithADifferentOrganizationsContextSet_SeesNothing()
    {
        var seeded = await SeedPerformanceAsync();

        var count = await CountAsPublicAsync(Guid.NewGuid(), seeded.PerformanceId);

        count.Should().Be(0);
    }
}
