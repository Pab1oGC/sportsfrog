using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// <see cref="TemplateWriter.ClearOtherDefaultsAsync"/> against a real
/// database, specifically where the catalog policy from
/// AddCertificateTemplateCatalog puts another organization's row inside what
/// a connection can see.
/// </summary>
/// <remarks>
/// Before <see cref="TemplateWriter"/>'s own OrgId filter, this query read
/// "every default of this kind", which — once a connection can see a catalog
/// row it does not own alongside its own — meant setting a new default for
/// one organization could try to flip another organization's catalog
/// default back off, and tenant_isolation's own WITH CHECK would refuse that
/// write outright. This pins the fix: an organization's own write never
/// reaches into the catalog it can only read.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TemplateWriterTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task ClearOtherDefaultsAsync_NeverTouchesAnotherOrganizationsCatalogDefault()
    {
        var platformOrgId = Guid.NewGuid();
        var tenantOrgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();
        setup.Organizations.Add(new Organization
        {
            Id = platformOrgId, Name = "Plataforma", Slug = $"platform-{platformOrgId:N}",
        });
        setup.Organizations.Add(new Organization
        {
            Id = tenantOrgId, Name = "Liga", Slug = $"liga-{tenantOrgId:N}",
        });
        await setup.SaveChangesAsync();

        var catalogDefault = await SeedTemplateAsync(
            platformOrgId, isCatalog: true, isDefault: true);
        var tenantsOwnDefault = await SeedTemplateAsync(
            tenantOrgId, isCatalog: false, isDefault: true);

        // The tenant's connection: it sees its own default (tenant_isolation)
        // and the platform's catalog default (the new catalog policy) at
        // once -- exactly the state that made the old, unfiltered query
        // reach the wrong row.
        await using var acting = fixture.CreateAppContext();
        await using var transaction = await acting.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(acting, tenantOrgId);

        var organization = new OrganizationContext();
        organization.Establish(tenantOrgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);
        var writer = new TemplateWriter(acting, organization);

        var newDefault = await acting.Set<DocumentTemplate>()
            .SingleAsync(candidate => candidate.Id == tenantsOwnDefault.Id);
        newDefault.IsDefault = true;

        await writer.ClearOtherDefaultsAsync(newDefault, CancellationToken.None);
        await acting.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var verify = fixture.CreateAppContext();

        var platformRow = await ReadAsAsync(verify, platformOrgId, catalogDefault.Id);
        platformRow.IsDefault.Should().BeTrue(
            "the catalog row belongs to a different organization and must never be written by it");

        var tenantRow = await ReadAsAsync(verify, tenantOrgId, tenantsOwnDefault.Id);
        tenantRow.IsDefault.Should().BeTrue("it is the one the write just promoted");
    }

    private static async Task<DocumentTemplate> ReadAsAsync(
        SportFrogDbContext database, Guid organizationId, Guid templateId)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(database, organizationId);

        return await database.Set<DocumentTemplate>().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == templateId);
    }

    private async Task<DocumentTemplate> SeedTemplateAsync(Guid orgId, bool isCatalog, bool isDefault)
    {
        await using var setup = fixture.CreateAppContext();
        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Kind = DocumentKind.Certificate,
            Name = $"Certificado {Guid.NewGuid():N}",
            Layout = new TemplateLayout(new TemplateFace(null, 1.41, []), null),
            PageSize = "a4",
            IsCatalog = isCatalog,
            IsDefault = isDefault,
            Version = 1,
        };
        setup.Set<DocumentTemplate>().Add(template);

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return template;
    }
}
