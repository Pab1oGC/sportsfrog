using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// <see cref="OrganizationContextExtensions.ReachesAllCompetitions"/>: the
/// check a bulk reschedule runs over its whole body before any of it is
/// applied, exercised with no route, no database and no HTTP pipeline —
/// the same way <see cref="RequireScopeExtensionsTests"/> exercises
/// <c>Reaches</c>.
/// </summary>
public sealed class OrganizationContextExtensionsTests
{
    [Fact]
    public void An_organization_wide_membership_reaches_every_competition()
    {
        var organization = OrganizationWide();

        organization.ReachesAllCompetitions([Guid.NewGuid(), Guid.NewGuid()]).Should().BeTrue();
    }

    [Fact]
    public void A_scoped_membership_reaches_a_batch_entirely_within_its_scope()
    {
        var competitionId = Guid.NewGuid();
        var organization = Established(competitionIds: [competitionId]);

        organization.ReachesAllCompetitions([competitionId, competitionId]).Should().BeTrue();
    }

    [Fact]
    public void A_scoped_membership_does_not_reach_a_batch_with_one_competition_outside_its_scope()
    {
        // The whole point: one out-of-scope target anywhere in the batch
        // refuses all of it, not just that one move.
        var inScope = Guid.NewGuid();
        var outOfScope = Guid.NewGuid();
        var organization = Established(competitionIds: [inScope]);

        organization.ReachesAllCompetitions([inScope, outOfScope]).Should().BeFalse();
    }

    [Fact]
    public void A_membership_scoped_to_no_competition_at_all_reaches_an_empty_batch()
    {
        // Vacuous truth, matching IEnumerable.All: a batch naming nothing
        // has nothing to be refused over.
        var organization = Established(competitionIds: []);

        organization.ReachesAllCompetitions([]).Should().BeTrue();
    }

    private static OrganizationContext Established(IEnumerable<Guid> competitionIds)
    {
        var organization = new OrganizationContext();
        organization.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        organization.EstablishScopes(new MembershipScopeSet(competitionIds.ToHashSet(), new HashSet<Guid>()));
        return organization;
    }

    private static OrganizationContext OrganizationWide()
    {
        var organization = new OrganizationContext();
        organization.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        organization.EstablishScopes(MembershipScopeSet.OrganizationWide);
        return organization;
    }
}
