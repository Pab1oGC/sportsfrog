using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;
using SportFrog.Api.Tests.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Infrastructure.Validation;

/// <summary>
/// Against a real query, not an in-memory stand-in — <c>ApplyAsync</c> exists
/// specifically to translate to SQL, and a fake <c>IQueryable</c> would prove
/// nothing about whether <c>Skip</c>/<c>Take</c>/<c>CountAsync</c> actually
/// do that.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PagedListingTests(SportFrogDatabaseFixture fixture)
{
    private async Task<Guid> SeedFiveClubsAsync()
    {
        var orgId = Guid.NewGuid();

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

        // Named so an alphabetical OrderBy gives a predictable order to page
        // through.
        for (var letter = 'A'; letter <= 'E'; letter++)
        {
            setup.Clubs.Add(new Club { Id = Guid.NewGuid(), OrgId = orgId, Name = $"Club {letter}" });
        }

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return orgId;
    }

    private async Task<(IReadOnlyList<Club> Page, DefaultHttpContext Context)> ApplyAsync(
        Guid orgId, int? skip, int? take)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var httpContext = new DefaultHttpContext();
        var query = context.Clubs.OrderBy(club => club.Name);

        var page = await PagedListing.ApplyAsync(query, httpContext, skip, take, CancellationToken.None);

        return (page, httpContext);
    }

    [Fact]
    public async Task ApplyAsync_NeitherSkipNorTakeGiven_ReturnsEveryRowAndSetsNoHeader()
    {
        var orgId = await SeedFiveClubsAsync();

        var (page, httpContext) = await ApplyAsync(orgId, skip: null, take: null);

        page.Should().HaveCount(5);
        httpContext.Response.Headers.Should().NotContainKey(PagedListing.TotalCountHeader);
    }

    [Fact]
    public async Task ApplyAsync_TakeGiven_ReturnsOnlyThatManyAndReportsTheRealTotal()
    {
        var orgId = await SeedFiveClubsAsync();

        var (page, httpContext) = await ApplyAsync(orgId, skip: null, take: 2);

        page.Should().HaveCount(2);
        page.Select(club => club.Name).Should().Equal("Club A", "Club B");
        httpContext.Response.Headers[PagedListing.TotalCountHeader].ToString().Should().Be("5");
    }

    [Fact]
    public async Task ApplyAsync_SkipAndTakeGiven_ReturnsTheRequestedPage()
    {
        var orgId = await SeedFiveClubsAsync();

        var (page, _) = await ApplyAsync(orgId, skip: 2, take: 2);

        page.Select(club => club.Name).Should().Equal("Club C", "Club D");
    }

    [Fact]
    public async Task ApplyAsync_SkipPastTheEnd_ReturnsAnEmptyPageRatherThanFailing()
    {
        var orgId = await SeedFiveClubsAsync();

        var (page, httpContext) = await ApplyAsync(orgId, skip: 50, take: 10);

        page.Should().BeEmpty();
        httpContext.Response.Headers[PagedListing.TotalCountHeader].ToString().Should().Be("5");
    }

    [Fact]
    public async Task ApplyAsync_ANegativeSkip_IsTreatedAsZero()
    {
        var orgId = await SeedFiveClubsAsync();

        var (page, _) = await ApplyAsync(orgId, skip: -10, take: 1);

        page.Select(club => club.Name).Should().Equal("Club A");
    }
}
