using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Features.Competitions;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// The visible number of a credential is claimed the way the print job claims
/// it: from a background scope that sets the organization, not from the
/// connection the job was built with. Without the scope the counter's row
/// security refuses the insert, and every card of a batch fails.
/// </summary>
/// <remarks>
/// The competition is committed rather than left inside a rolled-back
/// transaction, because the claim commits from its own connection and the
/// counter's foreign key needs to see the competition from there.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CredentialNumberingJobScopeTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task ClaimingInsideTheJobScope_HandsOutConsecutiveNumbers()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        var competitionId = await CommitCompetitionAsync(tenant);

        var services = new ServiceCollection();
        services.AddScoped<OrganizationContext>();
        services.AddScoped<CredentialNumbering>();

        await using var dataSource = SportFrogDataSource.Create(fixture.AppConnectionString);
        services.AddDbContext<SportFrogDbContext>(options =>
            options.UseNpgsql(dataSource, SportFrogDataSource.MapEnums));

        await using var provider = services.BuildServiceProvider();
        var scopes = new OrganizationJobScope(provider.GetRequiredService<IServiceScopeFactory>());
        var userId = Guid.NewGuid();

        var first = await scopes.RunAsync(
            tenant.OrgId, userId,
            (container, _) => container.GetRequiredService<CredentialNumbering>()
                .NextAsync(tenant.OrgId, competitionId, "CLB", "Club Ejemplo", CancellationToken.None),
            CancellationToken.None);

        var second = await scopes.RunAsync(
            tenant.OrgId, userId,
            (container, _) => container.GetRequiredService<CredentialNumbering>()
                .NextAsync(tenant.OrgId, competitionId, "CLB", "Club Ejemplo", CancellationToken.None),
            CancellationToken.None);

        Assert.Equal("CLB-0001", first);
        Assert.Equal("CLB-0002", second);
    }

    /// <summary>A competition committed from its own connection, visible to the claim.</summary>
    private async Task<Guid> CommitCompetitionAsync(CompetitionTenant tenant)
    {
        var competition = tenant.NewCompetition($"Copa Numeracion {Guid.NewGuid():N}");

        await using var setup = fixture.CreateAppContext();
        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, tenant.OrgId);

        setup.Competitions.Add(competition);
        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return competition.Id;
    }
}
