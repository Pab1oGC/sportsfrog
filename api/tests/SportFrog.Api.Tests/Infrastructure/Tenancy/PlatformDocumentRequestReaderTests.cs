using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Documents;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Infrastructure.Tenancy;

/// <summary>
/// <see cref="PlatformDocumentRequestReader"/> against a real database: it
/// has to cross tenant_isolation on purpose, so what matters here is not
/// mocked away by a fake context the way most feature tests can afford to.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class PlatformDocumentRequestReaderTests(SportFrogDatabaseFixture fixture)
{
    [Fact]
    public async Task ReadPendingAsync_CrossesEveryOrganization_AndOnlyReturnsPending()
    {
        var (orgA, pendingA) = await SeedRequestAsync(DocumentRequestState.Pending, DocumentKind.Credential);
        var (orgB, pendingB) = await SeedRequestAsync(DocumentRequestState.Pending, DocumentKind.Credential);
        var (_, cancelled) = await SeedRequestAsync(DocumentRequestState.Cancelled, DocumentKind.Credential);

        await using var reader = fixture.CreateAppContext();

        var pending = await PlatformDocumentRequestReader.ReadPendingAsync(reader, CancellationToken.None);

        pending.Should().Contain(p => p.Id == pendingA.Id && p.OrgId == orgA);
        pending.Should().Contain(p => p.Id == pendingB.Id && p.OrgId == orgB);
        pending.Should().NotContain(p => p.Id == cancelled.Id);
    }

    [Fact]
    public async Task ReadPendingAsync_NamesTheTemplateAndMotive_ForACertificateRequest()
    {
        var (orgId, pending) = await SeedRequestAsync(DocumentRequestState.Pending, DocumentKind.Credential);

        // The template's FK needs a real row of its own; seeded under the
        // same organization and transaction style SeedRequestAsync uses for
        // everything else this table references.
        var templateId = Guid.NewGuid();

        await using var withTemplate = fixture.CreateAppContext();
        await using var transaction = await withTemplate.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(withTemplate, orgId);

        withTemplate.Set<DocumentTemplate>().Add(new DocumentTemplate
        {
            Id = templateId,
            OrgId = orgId,
            Kind = DocumentKind.Certificate,
            Name = "Certificado",
            Layout = new TemplateLayout(new TemplateFace(null, 1.41, []), null),
            PageSize = "a4",
            Version = 1,
        });

        // Saved before the update below: template_id's FK has no navigation
        // property for EF to order these by on its own, and the template row
        // has to exist before anything can point at it.
        await withTemplate.SaveChangesAsync();

        var request = await withTemplate.DocumentRequests.SingleAsync(candidate => candidate.Id == pending.Id);
        request.Kind = DocumentKind.Certificate;
        request.TemplateId = templateId;
        request.CertificateType = "Participación";

        await withTemplate.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var reader = fixture.CreateAppContext();

        var rows = await PlatformDocumentRequestReader.ReadPendingAsync(reader, CancellationToken.None);

        var row = rows.Single(p => p.Id == pending.Id);
        row.Kind.Should().Be(DocumentKind.Certificate);
        row.TemplateId.Should().Be(templateId);
        row.CertificateType.Should().Be("Participación");
    }

    [Fact]
    public async Task ResolveOrganizationAsync_ReturnsTheOwningOrganization_RegardlessOfStatus()
    {
        var (orgId, request) = await SeedRequestAsync(DocumentRequestState.Rejected, DocumentKind.Credential);

        await using var reader = fixture.CreateAppContext();

        var resolved = await PlatformDocumentRequestReader.ResolveOrganizationAsync(
            reader, request.Id, CancellationToken.None);

        resolved.Should().Be(orgId);
    }

    [Fact]
    public async Task ResolveOrganizationAsync_ReturnsNull_ForAnUnknownId()
    {
        await using var reader = fixture.CreateAppContext();

        var resolved = await PlatformDocumentRequestReader.ResolveOrganizationAsync(
            reader, Guid.NewGuid(), CancellationToken.None);

        resolved.Should().BeNull();
    }

    private async Task<(Guid OrgId, DocumentRequest Request)> SeedRequestAsync(
        DocumentRequestState status,
        DocumentKind kind,
        Guid? templateId = null,
        string? certificateType = null)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Operador",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = "football",
            Name = "Reglamento",
            Config = new RulesetConfiguration
            {
                Periods = new PeriodRules { Count = 2, Label = "tiempo", Minutes = 45 },
                Points = new Dictionary<string, int> { ["win"] = 3, ["draw"] = 1, ["loss"] = 0 },
                Tiebreakers = [],
            },
        });

        setup.Competitions.Add(new Competition
        {
            Id = competitionId,
            OrgId = orgId,
            SportCode = "football",
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        var request = new DocumentRequest
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Kind = kind,
            TemplateId = templateId,
            CertificateType = certificateType,
            CompetitionId = competitionId,
            Status = status,
            RequestedBy = userId,
        };
        setup.DocumentRequests.Add(request);

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return (orgId, request);
    }
}
