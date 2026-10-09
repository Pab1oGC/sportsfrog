using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// <see cref="RequireScopeExtensions.Reaches"/>: the one branch of the whole
/// feature with real logic, exercised directly with no route, no database
/// and no HTTP pipeline — the same way <c>MembershipRoleAuthority.Reaches</c>
/// is tested.
/// </summary>
public sealed class RequireScopeExtensionsTests
{
    [Fact]
    public void Competition_axis_reaches_when_the_target_competition_is_in_scope()
    {
        var competitionId = Guid.NewGuid();
        var organization = EstablishedWith(competitionIds: [competitionId]);
        var target = new ResolvedScopeTarget(CompetitionId: competitionId, ClubId: null);

        RequireScopeExtensions.Reaches(ScopeType.Competition, organization, target).Should().BeTrue();
    }

    [Fact]
    public void Competition_axis_does_not_reach_a_different_competition()
    {
        var organization = EstablishedWith(competitionIds: [Guid.NewGuid()]);
        var target = new ResolvedScopeTarget(CompetitionId: Guid.NewGuid(), ClubId: null);

        RequireScopeExtensions.Reaches(ScopeType.Competition, organization, target).Should().BeFalse();
    }

    [Fact]
    public void Club_axis_reaches_when_the_target_club_is_in_scope()
    {
        var clubId = Guid.NewGuid();
        var organization = EstablishedWith(clubIds: [clubId]);
        var target = new ResolvedScopeTarget(CompetitionId: null, ClubId: clubId);

        RequireScopeExtensions.Reaches(ScopeType.Club, organization, target).Should().BeTrue();
    }

    [Fact]
    public void Club_axis_does_not_reach_a_different_club()
    {
        var organization = EstablishedWith(clubIds: [Guid.NewGuid()]);
        var target = new ResolvedScopeTarget(CompetitionId: null, ClubId: Guid.NewGuid());

        RequireScopeExtensions.Reaches(ScopeType.Club, organization, target).Should().BeFalse();
    }

    [Fact]
    public void Competition_axis_ignores_a_matching_club_id_on_the_same_target()
    {
        // A team belongs to both a competition and a club at once. Checking
        // the competition axis must look only at CompetitionId, never let a
        // club match stand in for it even when both happen to be present.
        var clubId = Guid.NewGuid();
        var organization = EstablishedWith(clubIds: [clubId]);
        var target = new ResolvedScopeTarget(CompetitionId: Guid.NewGuid(), ClubId: clubId);

        RequireScopeExtensions.Reaches(ScopeType.Competition, organization, target).Should().BeFalse();
    }

    [Fact]
    public void Club_axis_ignores_a_matching_competition_id_on_the_same_target()
    {
        var competitionId = Guid.NewGuid();
        var organization = EstablishedWith(competitionIds: [competitionId]);
        var target = new ResolvedScopeTarget(CompetitionId: competitionId, ClubId: Guid.NewGuid());

        RequireScopeExtensions.Reaches(ScopeType.Club, organization, target).Should().BeFalse();
    }

    [Fact]
    public void Competition_axis_does_not_reach_a_target_with_no_competition_id()
    {
        // A club-only target (ScopeTargetKind.Club) resolves CompetitionId
        // to null. The competition axis must refuse it even if, implausibly,
        // the caller's scope set were non-empty.
        var organization = EstablishedWith(competitionIds: [Guid.NewGuid()]);
        var target = new ResolvedScopeTarget(CompetitionId: null, ClubId: null);

        RequireScopeExtensions.Reaches(ScopeType.Competition, organization, target).Should().BeFalse();
    }

    private static OrganizationContext EstablishedWith(
        IEnumerable<Guid>? competitionIds = null, IEnumerable<Guid>? clubIds = null)
    {
        var organization = new OrganizationContext();
        organization.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        organization.EstablishScopes(new MembershipScopeSet(
            (competitionIds ?? []).ToHashSet(),
            (clubIds ?? []).ToHashSet()));
        return organization;
    }
}
