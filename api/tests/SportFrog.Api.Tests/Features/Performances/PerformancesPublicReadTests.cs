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
/// Updated by ScopePublicReadsToPublishedCompetitions: performances used to
/// carry the grant but no policy of their own, so <c>sportfrog_public</c>
/// only ever saw one through the same broad <c>tenant_isolation</c> rule
/// every table had — visible whenever the caller's own context happened to
/// match, published or not. That was the actual gap; matching context was
/// never the real boundary anywhere else public reads scopes itself by
/// (<c>published_competitions</c>, <c>published_matches</c>,
/// <c>published_teams</c> all key on <c>is_public</c> alone, with no org
/// check at all — a visitor is never asked which organization they're
/// looking for, and this platform's public directory spans every
/// organization that has published something, by design). Performances now
/// follows that same, already-established shape:
/// <c>published_performances</c> keys on the owning competition's
/// <c>is_public</c>, not on <c>app.current_org</c>. The boundary these tests
/// prove is publication, not organization context — matching context no
/// longer matters, and no longer should.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PerformancesPublicReadTests(SportFrogDatabaseFixture fixture)
{
    private sealed record Seeded(Guid OrgId, Guid PerformanceId);

    private async Task<Seeded> SeedPerformanceAsync(bool competitionIsPublic = true)
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
            IsPublic = competitionIsPublic,
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

    /// <summary>
    /// No context is exactly how the real public portal reads this row:
    /// <c>ReadPublicClassification</c> never sets <c>app.current_org</c> for
    /// <c>PerformancesQuery</c> at all, because <c>published_performances</c>
    /// does not need it — the same as every other published_* policy.
    /// </summary>
    [Fact]
    public async Task SportfrogPublic_WithNoOrganizationContext_SeesThePublishedRow()
    {
        var seeded = await SeedPerformanceAsync(competitionIsPublic: true);

        var count = await CountAsPublicAsync(organizationContext: null, seeded.PerformanceId);

        count.Should().Be(1);
    }

    /// <summary>
    /// Publication, not a matching organization, is the real boundary: a
    /// visitor claiming to be an unrelated organization still sees a
    /// performance whose own competition chose to publish it — the same
    /// cross-organization directory shape <c>published_competitions</c>
    /// already has. A future change that starts scoping this by
    /// organization would need to change <c>published_competitions</c> and
    /// its siblings too, deliberately, not by accident here.
    /// </summary>
    [Fact]
    public async Task SportfrogPublic_WithAnUnrelatedOrganizationsContextSet_StillSeesThePublishedRow()
    {
        var seeded = await SeedPerformanceAsync(competitionIsPublic: true);

        var count = await CountAsPublicAsync(Guid.NewGuid(), seeded.PerformanceId);

        count.Should().Be(1);
    }

    /// <summary>
    /// The boundary this table actually needs: a performance whose
    /// competition was never published stays invisible to
    /// <c>sportfrog_public</c> no matter what context is or isn't set —
    /// proven both ways in one test so a fix that satisfies only one of
    /// them doesn't read as green.
    /// </summary>
    [Fact]
    public async Task SportfrogPublic_WhenTheCompetitionIsNotPublished_SeesNothingRegardlessOfContext()
    {
        var seeded = await SeedPerformanceAsync(competitionIsPublic: false);

        var withNoContext = await CountAsPublicAsync(organizationContext: null, seeded.PerformanceId);
        var withOwningContext = await CountAsPublicAsync(seeded.OrgId, seeded.PerformanceId);

        withNoContext.Should().Be(0);
        withOwningContext.Should().Be(0);
    }
}
