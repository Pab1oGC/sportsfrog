using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// The ordering <see cref="OrganizationContextMiddleware"/> depends on:
/// scopes can only be established once the organization itself has been,
/// and only once per request — the same two guarantees <c>Establish</c>
/// already gave the role.
/// </summary>
public sealed class OrganizationContextTests
{
    private static readonly MembershipScopeSet SomeScopes =
        new(new HashSet<Guid> { Guid.NewGuid() }, new HashSet<Guid>());

    [Fact]
    public void EstablishScopes_BeforeEstablish_Throws()
    {
        var context = new OrganizationContext();

        var act = () => context.EstablishScopes(SomeScopes);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EstablishScopes_CalledTwice_Throws()
    {
        var context = new OrganizationContext();
        context.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        context.EstablishScopes(SomeScopes);

        var act = () => context.EstablishScopes(SomeScopes);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EstablishScopes_CalledOnceWithNoRows_ReadsAsOrganizationWide()
    {
        // The case that matters most: an org-wide membership must not look
        // "not yet read" just because both sets come back empty. If this
        // regressed to the count-based guard, a second call with another
        // empty result would silently succeed instead of throwing.
        var context = new OrganizationContext();
        context.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);

        context.EstablishScopes(MembershipScopeSet.OrganizationWide);

        context.IsOrganizationWide.Should().BeTrue();
        context.ScopedCompetitionIds.Should().BeEmpty();
        context.ScopedClubIds.Should().BeEmpty();

        var act = () => context.EstablishScopes(MembershipScopeSet.OrganizationWide);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EstablishScopes_WithRows_ExposesThemAndIsNotOrganizationWide()
    {
        var context = new OrganizationContext();
        context.Establish(Guid.NewGuid(), MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);

        context.EstablishScopes(SomeScopes);

        context.IsOrganizationWide.Should().BeFalse();
        context.ScopedCompetitionIds.Should().BeEquivalentTo(SomeScopes.CompetitionIds);
        context.ScopedClubIds.Should().BeEmpty();
    }

    [Fact]
    public void A_fresh_context_IsOrganizationWide_before_anything_is_established()
    {
        // Matters because IsEstablished itself is false at this point: the
        // property has to read as a sensible default rather than throw or
        // give a misleading answer before Establish ever runs.
        var context = new OrganizationContext();

        context.IsOrganizationWide.Should().BeTrue();
    }
}
