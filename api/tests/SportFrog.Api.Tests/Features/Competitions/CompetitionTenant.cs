using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Competitions;

/// <summary>
/// An organization with one ruleset, the least a competition needs to exist,
/// and a way to act inside it the way a request does: one transaction with the
/// isolation context set. Shared by the tests that pin a competition's
/// uniqueness at the index and at the endpoint, so both seed the same way.
/// </summary>
internal sealed record CompetitionTenant(Guid OrgId, Guid RulesetId)
{
    public static async Task<CompetitionTenant> SeedAsync(SportFrogDatabaseFixture fixture)
    {
        var orgId = Guid.NewGuid();
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

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CompetitionTenant(orgId, rulesetId);
    }

    public Competition NewCompetition(string name, string? slug = null)
    {
        var id = Guid.NewGuid();

        return new Competition
        {
            Id = id,
            OrgId = OrgId,
            SportCode = "football",
            RulesetId = RulesetId,
            Name = name,
            Slug = slug ?? $"comp-{id:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        };
    }

    /// <summary>
    /// A context inside this organization, in a transaction that is rolled
    /// back when the scope is disposed, so a test leaves nothing behind.
    /// </summary>
    public async Task<Scope> OpenAsync(SportFrogDatabaseFixture fixture)
    {
        var context = fixture.CreateAppContext();
        var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, OrgId);

        return new Scope(context, transaction);
    }

    public sealed class Scope(SportFrogDbContext context, IAsyncDisposable transaction) : IAsyncDisposable
    {
        public SportFrogDbContext Context => context;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await context.DisposeAsync();
        }
    }
}
