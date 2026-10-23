using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// <see cref="PlatformTemplateReader"/> against a real database: it has to
/// cross tenant_isolation on purpose, the same reason
/// <see cref="PlatformCredentialRequestReaderTests"/> does not mock it away.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PlatformTemplateReaderTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task ResolveOrganizationAsync_ReturnsTheOwningOrganization_ForABespokeDesign()
    {
        var (orgId, template) = await SeedTemplateAsync(isCatalog: false);

        await using var reader = fixture.CreateAppContext();

        var resolved = await PlatformTemplateReader.ResolveOrganizationAsync(
            reader, template.Id, CancellationToken.None);

        resolved.Should().Be(orgId);
    }

    [Fact]
    public async Task ResolveOrganizationAsync_ReturnsTheOwningOrganization_ForACatalogDesign()
    {
        var (orgId, template) = await SeedTemplateAsync(isCatalog: true);

        await using var reader = fixture.CreateAppContext();

        var resolved = await PlatformTemplateReader.ResolveOrganizationAsync(
            reader, template.Id, CancellationToken.None);

        resolved.Should().Be(orgId);
    }

    [Fact]
    public async Task ResolveOrganizationAsync_ReturnsNull_ForAnUnknownId()
    {
        await using var reader = fixture.CreateAppContext();

        var resolved = await PlatformTemplateReader.ResolveOrganizationAsync(
            reader, Guid.NewGuid(), CancellationToken.None);

        resolved.Should().BeNull();
    }

    private async Task<(Guid OrgId, DocumentTemplate Template)> SeedTemplateAsync(bool isCatalog)
    {
        var orgId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Kind = DocumentKind.Certificate,
            Name = "Certificado",
            Layout = new TemplateLayout(new TemplateFace(null, 1.41, []), null),
            PageSize = "a4",
            IsCatalog = isCatalog,
            Version = 1,
        };
        setup.Set<DocumentTemplate>().Add(template);

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return (orgId, template);
    }
}
