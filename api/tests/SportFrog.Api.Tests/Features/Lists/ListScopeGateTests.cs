using AwesomeAssertions;
using SportFrog.Api.Features.Lists;
using SportFrog.Api.Features.Lists.Providers;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// Whether a scoped membership may run one list at all — driven by
/// <see cref="ListParameterKind"/>, never by which provider declares it:
/// <see cref="CardsList"/> stands in for every provider with a
/// <see cref="ListParameterKind.Category"/> parameter, <see cref="RosterList"/>
/// for the one with a <see cref="ListParameterKind.Team"/> parameter, and
/// <see cref="AthletesList"/> for the one with neither.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ListScopeGateTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task An_organization_wide_membership_reaches_every_list_with_no_scope_read_at_all()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(MembershipScopeSet.OrganizationWide);

        // Deliberately an empty scope -- CardsList requires categoryId, but
        // an organization-wide membership must short-circuit before ever
        // reading it.
        var reaches = await ReachesAsync(new CardsList(), ListScope.Empty, organization);

        reaches.Should().BeTrue();
    }

    [Fact]
    public async Task A_category_list_reaches_a_category_scoped_to_its_own_competition()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid> { fx.CompetitionId }, new HashSet<Guid>()));

        var reaches = await ReachesAsync(new CardsList(), ScopeWith("categoryId", fx.CategoryId), organization);

        reaches.Should().BeTrue();
    }

    [Fact]
    public async Task A_category_list_does_not_reach_a_category_of_a_different_competition()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid>()));

        var reaches = await ReachesAsync(new CardsList(), ScopeWith("categoryId", fx.CategoryId), organization);

        reaches.Should().BeFalse();
    }

    [Fact]
    public async Task A_category_list_refuses_a_scoped_membership_with_no_categoryId_given()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid> { fx.CompetitionId }, new HashSet<Guid>()));

        var reaches = await ReachesAsync(new CardsList(), ListScope.Empty, organization);

        reaches.Should().BeFalse();
    }

    [Fact]
    public async Task A_team_list_reaches_a_team_through_the_competition_axis()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid> { fx.CompetitionId }, new HashSet<Guid>()));

        var reaches = await ReachesAsync(new RosterList(), ScopeWith("teamId", fx.TeamId), organization);

        reaches.Should().BeTrue();
    }

    [Fact]
    public async Task A_team_list_reaches_a_team_through_the_club_axis()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid>(), new HashSet<Guid> { fx.ClubId }));

        var reaches = await ReachesAsync(new RosterList(), ScopeWith("teamId", fx.TeamId), organization);

        reaches.Should().BeTrue();
    }

    [Fact]
    public async Task A_team_list_does_not_reach_a_team_scoped_to_neither_axis()
    {
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(
            new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid> { Guid.NewGuid() }));

        var reaches = await ReachesAsync(new RosterList(), ScopeWith("teamId", fx.TeamId), organization);

        reaches.Should().BeFalse();
    }

    [Fact]
    public async Task A_list_with_neither_a_category_nor_a_team_parameter_is_let_through_unconditionally()
    {
        // AthletesList has nothing here to gate on a single target --
        // ReadLists narrows its rows for a scoped membership instead of
        // asking this class to refuse it outright.
        var fx = await SeedAsync();
        var organization = Established(fx.OrgId);
        organization.EstablishScopes(new MembershipScopeSet(new HashSet<Guid>(), new HashSet<Guid> { fx.ClubId }));

        var reaches = await ReachesAsync(new AthletesList(), ListScope.Empty, organization);

        reaches.Should().BeTrue();
    }

    private static ListScope ScopeWith(string name, Guid value) =>
        new(new Dictionary<string, string> { [name] = value.ToString() });

    private static OrganizationContext Established(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        return organization;
    }

    private async Task<bool> ReachesAsync(IListProvider provider, ListScope scope, OrganizationContext organization)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, organization.RequireOrganizationId());

        var resolver = new ScopeResolver(context);
        var result = await ListScopeGate.ReachesAsync(provider, scope, organization, resolver, CancellationToken.None);

        await transaction.CommitAsync();
        return result;
    }

    private sealed record Fixture(Guid OrgId, Guid ClubId, Guid CompetitionId, Guid CategoryId, Guid TeamId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

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

        setup.Categories.Add(new Category
        {
            Id = categoryId,
            OrgId = orgId,
            CompetitionId = competitionId,
            Name = "Categoria",
        });

        setup.Teams.Add(new Team
        {
            Id = teamId,
            OrgId = orgId,
            ClubId = clubId,
            CategoryId = categoryId,
            Name = "Equipo de Prueba",
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, clubId, competitionId, categoryId, teamId);
    }
}
