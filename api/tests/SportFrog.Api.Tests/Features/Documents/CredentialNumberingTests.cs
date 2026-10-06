using AwesomeAssertions;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// Claiming a credential's visible number — the one genuinely concurrent
/// write in this feature, since <c>IssueDocumentsJob</c> itself holds no
/// transaction across the slow middle of a batch while several could claim
/// numbers for the same competition at once.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CredentialNumberingTests(SportFrogDatabaseFixture fixture)
{
    private async Task<(Guid OrgId, Guid CompetitionId)> SeedCompetitionAsync()
    {
        var orgId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();

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

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return (orgId, competitionId);
    }

    [Fact]
    public async Task NextAsync_FirstClaimForACompetition_StartsAtOne()
    {
        var (orgId, competitionId) = await SeedCompetitionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var id = await new CredentialNumbering(context)
            .NextAsync(orgId, competitionId, "IND", "Club Atlético Independiente", CancellationToken.None);

        id.Should().Be("IND-0001");
    }

    [Fact]
    public async Task NextAsync_CalledRepeatedly_IncrementsByOneEachTime()
    {
        var (orgId, competitionId) = await SeedCompetitionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var numbering = new CredentialNumbering(context);

        var first = await numbering.NextAsync(orgId, competitionId, "IND", "Independiente", CancellationToken.None);
        var second = await numbering.NextAsync(orgId, competitionId, "IND", "Independiente", CancellationToken.None);
        var third = await numbering.NextAsync(orgId, competitionId, "IND", "Independiente", CancellationToken.None);

        first.Should().Be("IND-0001");
        second.Should().Be("IND-0002");
        third.Should().Be("IND-0003");
    }

    [Fact]
    public async Task NextAsync_DifferentClubsSameCompetition_ShareOneSequence()
    {
        // The number answers "the Nth credential this competition issued",
        // not "the Nth for this club" — see VisibleCredentialId's own
        // remarks on why the two kinds of identifier a card carries answer
        // different questions.
        var (orgId, competitionId) = await SeedCompetitionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var numbering = new CredentialNumbering(context);

        var independiente = await numbering.NextAsync(orgId, competitionId, "IND", "Independiente", CancellationToken.None);
        var barcelona = await numbering.NextAsync(orgId, competitionId, "BAR", "Barcelona", CancellationToken.None);

        independiente.Should().Be("IND-0001");
        barcelona.Should().Be("BAR-0002");
    }

    [Fact]
    public async Task NextAsync_TwoDifferentCompetitions_EachKeepsItsOwnSequence()
    {
        var (orgId, competitionOneId) = await SeedCompetitionAsync();
        var (_, competitionTwoId) = await SeedCompetitionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var numbering = new CredentialNumbering(context);

        // Claim twice in the first competition, once in the second — if the
        // counters were shared, the second competition's one claim would
        // come out "0003" instead of "0001".
        await numbering.NextAsync(orgId, competitionOneId, "IND", "Independiente", CancellationToken.None);
        await numbering.NextAsync(orgId, competitionOneId, "IND", "Independiente", CancellationToken.None);
        var secondCompetitionFirst = await numbering.NextAsync(
            orgId, competitionTwoId, "IND", "Independiente", CancellationToken.None);

        secondCompetitionFirst.Should().Be("IND-0001");
    }

    [Fact]
    public async Task NextAsync_ClubHasNoShortName_FallsBackToTheFullNamesInitials()
    {
        var (orgId, competitionId) = await SeedCompetitionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var id = await new CredentialNumbering(context)
            .NextAsync(orgId, competitionId, null, "Barcelona Sporting Club", CancellationToken.None);

        id.Should().Be("BAR-0001");
    }
}
