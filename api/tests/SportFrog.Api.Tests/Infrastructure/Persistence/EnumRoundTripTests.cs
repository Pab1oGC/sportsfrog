using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// RF-02 — the most fragile point of the mapping: if the translation to
/// Postgres' <c>membership_role</c> enum is wrong, it fails here, not the
/// first time a role gets read back in production.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class EnumRoundTripTests(SportFrogDatabaseFixture fixture)
{
    [Theory]
    [InlineData(MembershipRole.Owner)]
    [InlineData(MembershipRole.Admin)]
    [InlineData(MembershipRole.Operator)]
    [InlineData(MembershipRole.Recorder)]
    [InlineData(MembershipRole.Viewer)]
    public async Task MembershipRole_RoundTripsThroughThePostgresEnum(MembershipRole role)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        await using (var setup = fixture.CreateAppContext())
        {
            // organizations and users carry no isolation policy: no
            // transaction/context dance needed for these two inserts.
            setup.Organizations.Add(new Organization
            {
                Id = organizationId,
                Name = $"Org {organizationId:N}",
                Slug = $"org-{organizationId:N}",
            });
            setup.Users.Add(new User
            {
                Id = userId,
                Email = $"{userId:N}@test.sportfrog.local",
                PasswordHash = "irrelevant-for-this-test",
                FullName = "Test User",
            });
            await setup.SaveChangesAsync();

            // organization_memberships is RLS-protected: set_config(..., true)
            // only lasts for the current transaction, so it and the INSERT it
            // gates must share one, or WITH CHECK sees no context and rejects
            // the row.
            await using var transaction = await setup.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, organizationId);

            setup.Memberships.Add(new OrganizationMembership
            {
                Id = membershipId,
                OrgId = organizationId,
                UserId = userId,
                Role = role,
            });
            await setup.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // A fresh context: reading back through EF's first-level cache would
        // return the in-memory instance instead of re-parsing what Postgres
        // actually stored, proving nothing about the enum converter.
        await using var verification = fixture.CreateAppContext();
        await using var verificationTransaction = await verification.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(verification, organizationId);

        var reread = await verification.Memberships.SingleAsync(m => m.Id == membershipId);
        await verificationTransaction.CommitAsync();

        reread.Role.Should().Be(role);
    }
}
