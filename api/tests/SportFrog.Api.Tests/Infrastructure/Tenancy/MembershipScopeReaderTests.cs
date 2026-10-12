using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// What <see cref="OrganizationContextMiddleware"/> relies on to answer "is
/// this caller narrowed, and to what": a membership with no rows in
/// <c>membership_scopes</c> must read as organization-wide, one with rows
/// must read exactly those targets, and scopes belonging to a different
/// organization or a different person must never appear — the last of
/// which is the row-level isolation policy's job, not a filter this class
/// adds on top of it.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class MembershipScopeReaderTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task A_membership_with_no_scope_rows_reads_as_organization_wide()
    {
        var fx = await SeedAsync();

        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.IsOrganizationWide.Should().BeTrue();
        scopes.CompetitionIds.Should().BeEmpty();
        scopes.ClubIds.Should().BeEmpty();
    }

    [Fact]
    public async Task A_membership_scoped_to_one_competition_reads_exactly_that_competition()
    {
        var fx = await SeedAsync();
        await AddScopeAsync(fx.OrgId, fx.MembershipId, ScopeType.Competition, competitionId: fx.CompetitionId);

        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.IsOrganizationWide.Should().BeFalse();
        scopes.CompetitionIds.Should().BeEquivalentTo([fx.CompetitionId]);
        scopes.ClubIds.Should().BeEmpty();
    }

    [Fact]
    public async Task A_membership_scoped_to_one_club_reads_exactly_that_club()
    {
        var fx = await SeedAsync();
        await AddScopeAsync(fx.OrgId, fx.MembershipId, ScopeType.Club, clubId: fx.ClubId);

        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.IsOrganizationWide.Should().BeFalse();
        scopes.ClubIds.Should().BeEquivalentTo([fx.ClubId]);
        scopes.CompetitionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task A_membership_scoped_to_both_a_competition_and_a_club_reads_both()
    {
        // The two kinds are independent: a delegate for one club who is also
        // that event's own administrator is one membership with one row of
        // each kind, not a conflict between them.
        var fx = await SeedAsync();
        await AddScopeAsync(fx.OrgId, fx.MembershipId, ScopeType.Competition, competitionId: fx.CompetitionId);
        await AddScopeAsync(fx.OrgId, fx.MembershipId, ScopeType.Club, clubId: fx.ClubId);

        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.CompetitionIds.Should().BeEquivalentTo([fx.CompetitionId]);
        scopes.ClubIds.Should().BeEquivalentTo([fx.ClubId]);
    }

    [Fact]
    public async Task A_scope_belonging_to_another_persons_membership_in_the_same_organization_is_not_read()
    {
        var fx = await SeedAsync();
        var otherUserId = Guid.NewGuid();
        var otherMembershipId = await AddMembershipForNewUserAsync(fx.OrgId, otherUserId);
        await AddScopeAsync(fx.OrgId, otherMembershipId, ScopeType.Competition, competitionId: fx.CompetitionId);

        // fx's own membership still has no rows: reading for fx.UserId must
        // not pick up what was granted to somebody else in the same org.
        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.IsOrganizationWide.Should().BeTrue();
    }

    [Fact]
    public async Task A_scope_in_a_different_organization_is_not_read_even_for_the_same_person()
    {
        // Row-level security, not an application-level filter, is what has
        // to make this true: both memberships belong to the same user, and
        // nothing in the LINQ query below filters by organization at all.
        var fx = await SeedAsync();
        var otherOrg = await SeedAsync();
        var sameUserOtherMembership = await AddMembershipForExistingUserAsync(otherOrg.OrgId, fx.UserId);
        await AddScopeAsync(otherOrg.OrgId, sameUserOtherMembership, ScopeType.Club, clubId: otherOrg.ClubId);

        var scopes = await ReadAsync(fx.OrgId, fx.UserId);

        scopes.IsOrganizationWide.Should().BeTrue();
    }

    private async Task<MembershipScopeSet> ReadAsync(Guid organizationId, Guid userId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, organizationId);

        var reader = new MembershipScopeReader(context);
        var result = await reader.ReadAsync(userId, CancellationToken.None);

        await transaction.CommitAsync();
        return result;
    }

    private async Task AddScopeAsync(
        Guid organizationId, Guid membershipId, ScopeType scopeType, Guid? competitionId = null, Guid? clubId = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, organizationId);

        context.MembershipScopes.Add(new MembershipScope
        {
            Id = Guid.NewGuid(),
            OrgId = organizationId,
            MembershipId = membershipId,
            ScopeType = scopeType,
            CompetitionId = competitionId,
            ClubId = clubId,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>Adds a brand new person to an organization, with a membership of their own.</summary>
    private async Task<Guid> AddMembershipForNewUserAsync(Guid organizationId, Guid userId)
    {
        await using var write = fixture.CreateAppContext();

        write.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@test.sportfrog.local",
            PasswordHash = "irrelevant-for-this-test",
            FullName = "Test User",
        });
        await write.SaveChangesAsync();

        return await AddMembershipForExistingUserAsync(organizationId, userId);
    }

    /// <summary>
    /// Adds a second membership, in another organization, for a person who
    /// already has an account — the real RF-02 case of a referee or
    /// delegate holding a role in more than one league.
    /// </summary>
    private async Task<Guid> AddMembershipForExistingUserAsync(Guid organizationId, Guid userId)
    {
        await using var write = fixture.CreateAppContext();
        await using var transaction = await write.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(write, organizationId);

        var membershipId = Guid.NewGuid();
        write.Memberships.Add(new OrganizationMembership
        {
            Id = membershipId,
            OrgId = organizationId,
            UserId = userId,
            Role = MembershipRole.Operator,
        });
        await write.SaveChangesAsync();
        await transaction.CommitAsync();

        return membershipId;
    }

    private sealed record Fixture(Guid OrgId, Guid UserId, Guid MembershipId, Guid CompetitionId, Guid ClubId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        setup.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@test.sportfrog.local",
            PasswordHash = "irrelevant-for-this-test",
            FullName = "Test User",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        var membershipId = Guid.NewGuid();
        setup.Memberships.Add(new OrganizationMembership
        {
            Id = membershipId,
            OrgId = orgId,
            UserId = userId,
            Role = MembershipRole.Operator,
        });

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club de Prueba" });

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

        return new Fixture(orgId, userId, membershipId, competitionId, clubId);
    }
}
