using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// RF-02 — soft-deleted organizations disappear from ordinary queries by
/// EF's own global filter, not by a row-level security policy: this table
/// carries none. <see cref="DbContext.IgnoreQueryFilters"/> must still reach
/// them, since that's what an administrative recovery query needs.
///
/// Isolation note: organizations carries no RLS policy, so every other
/// test's rows are visible here too. Every assertion below is scoped to this
/// test's own ids — never to a full-table read or count.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class SoftDeleteQueryFilterTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task Organizations_WithDeletedAt_AreExcludedByDefault()
    {
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await using (var setup = fixture.CreateAppContext())
        {
            setup.Organizations.Add(new Organization
            {
                Id = activeId,
                Name = "Active Org",
                Slug = $"org-{activeId:N}",
            });
            setup.Organizations.Add(new Organization
            {
                Id = deletedId,
                Name = "Deleted Org",
                Slug = $"org-{deletedId:N}",
                DeletedAt = DateTimeOffset.UtcNow,
            });
            await setup.SaveChangesAsync();
        }

        await using var context = fixture.CreateAppContext();

        var visibleIds = await context.Organizations
            .Where(o => o.Id == activeId || o.Id == deletedId)
            .Select(o => o.Id)
            .ToListAsync();

        visibleIds.Should().BeEquivalentTo([activeId]);
    }

    [Fact]
    public async Task Organizations_WithDeletedAt_AreIncluded_WithIgnoreQueryFilters()
    {
        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var deletedAt = DateTimeOffset.UtcNow;

        await using (var setup = fixture.CreateAppContext())
        {
            setup.Organizations.Add(new Organization
            {
                Id = activeId,
                Name = "Active Org",
                Slug = $"org-{activeId:N}",
            });
            setup.Organizations.Add(new Organization
            {
                Id = deletedId,
                Name = "Deleted Org",
                Slug = $"org-{deletedId:N}",
                DeletedAt = deletedAt,
            });
            await setup.SaveChangesAsync();
        }

        await using var context = fixture.CreateAppContext();

        var visible = await context.Organizations
            .IgnoreQueryFilters()
            .Where(o => o.Id == activeId || o.Id == deletedId)
            .ToListAsync();

        visible.Select(o => o.Id).Should().BeEquivalentTo([activeId, deletedId]);
        visible.Single(o => o.Id == deletedId).DeletedAt.Should().NotBeNull();
    }
}
