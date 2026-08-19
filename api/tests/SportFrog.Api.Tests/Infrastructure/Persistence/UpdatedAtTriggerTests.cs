using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// RF-02 — <c>updated_at</c> is owned by the database: a default on insert,
/// the <c>touch_updated_at</c> trigger on update. EF only ever reads it back.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class UpdatedAtTriggerTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task UpdatedAt_ChangesAfterAnUpdate_WithoutEFWritingItItself()
    {
        var organizationId = Guid.NewGuid();

        // organizations carries no isolation policy: no transaction/context
        // dance needed for either the insert or the update below.
        await using var insertContext = fixture.CreateAppContext();
        insertContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Original Name",
            Slug = $"org-{organizationId:N}",
        });
        await insertContext.SaveChangesAsync();

        var insertedAt = (await insertContext.Organizations.SingleAsync(o => o.Id == organizationId)).UpdatedAt;

        // A separate SaveChanges call, not a shared transaction: Postgres'
        // now() is constant for the life of one transaction, so an insert
        // and an update sharing one would get an identical timestamp and
        // this test would prove nothing.
        await using var updateContext = fixture.CreateAppContext();
        var organization = await updateContext.Organizations.SingleAsync(o => o.Id == organizationId);
        organization.Name = "Renamed";
        await updateContext.SaveChangesAsync();

        // EF never sends updated_at in the UPDATE (it's mapped
        // ValueGeneratedOnAddOrUpdate); this passing proves it picked up the
        // trigger-written value via RETURNING rather than leaving it stale.
        organization.UpdatedAt.Should().BeAfter(insertedAt);

        await using var rereadContext = fixture.CreateAppContext();
        var reread = await rereadContext.Organizations.SingleAsync(o => o.Id == organizationId);
        reread.UpdatedAt.Should().Be(organization.UpdatedAt);
    }
}
