using AwesomeAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// The gate <see cref="RequestDocumentBatch"/> runs before anything else:
/// only the platform organization itself may raise a batch, for either
/// kind.
/// </summary>
/// <remarks>
/// Checked against the organization active for the request, not a role
/// claim — see the gate's own remarks for why a claim alone would leave a
/// hole for an account that also holds a role in some other organization.
/// This test proves the fix by never presenting any claim at all: the
/// <see cref="OrganizationContext"/> built here carries no token and no
/// role claim for the platform organization, only an active organization —
/// so if the gate were (still, or again) a claim check, this would wrongly
/// pass for the first case and wrongly fail for the second.
///
/// Isolation note: the platform organization is the one <c>SeedPlatformOwner</c>
/// seeds once for the whole collection (see <see cref="PlatformAuthorityTests"/>
/// for the same note) — read here, never inserted a second time, which
/// would collide with its own unique slug.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class RequestDocumentBatchPlatformGateTests(SportFrogDatabaseFixture fixture)
{
    /// <summary>
    /// <c>HandleAsync</c> only ever queues through
    /// <see cref="HttpResponse.OnCompleted"/>, which a bare
    /// <see cref="DefaultHttpContext"/> never fires outside a real response
    /// pipeline — nothing in either test below reaches that far regardless.
    /// </summary>
    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    [Fact]
    public async Task AnOrdinaryOrganizationActingAsItself_IsRefusedWithForbidden()
    {
        var ordinaryOrgId = Guid.NewGuid();

        var organization = new OrganizationContext();
        organization.Establish(ordinaryOrgId, MembershipRole.Owner, Guid.NewGuid(), ipAddress: null);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, ordinaryOrgId);

        var result = await RequestDocumentBatch.HandleAsync(
            new RequestDocumentBatch.Request("credential", null, Guid.NewGuid()),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ThePlatformOrganizationActingAsItself_PassesTheGate()
    {
        var platformOrgId = await PlatformOrganizationIdAsync();

        var organization = new OrganizationContext();
        organization.Establish(platformOrgId, MembershipRole.Owner, Guid.NewGuid(), ipAddress: null);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, platformOrgId);

        var result = await RequestDocumentBatch.HandleAsync(
            new RequestDocumentBatch.Request("credential", null, Guid.NewGuid()),
            new DefaultHttpContext(),
            context,
            organization,
            new RecordingBackgroundJobClient(),
            CancellationToken.None);

        // Past the gate, a competition that does not exist is refused on
        // its own terms: ValidationProblem, status 400 -- never 403. Both
        // happen to share ProblemHttpResult as their concrete type, so the
        // status code is what actually distinguishes "the gate let this
        // through" from "the gate refused it".
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().NotBe(StatusCodes.Status403Forbidden);
    }

    private async Task<Guid> PlatformOrganizationIdAsync()
    {
        await using var context = fixture.CreateAppContext();

        return await context.Organizations
            .Where(organization => organization.Slug == PlatformAuthority.OrganizationSlug)
            .Select(organization => organization.Id)
            .SingleAsync();
    }
}
