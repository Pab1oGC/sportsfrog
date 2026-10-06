using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Features.Competitions;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// Credential designs against a real database: which one is the default, and
/// which values a batch freezes from the design a competition chose.
/// </summary>
/// <remarks>
/// Only keys are used throughout. A data URL would need the picture store,
/// which none of these rules depend on, so the store is passed as null.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CredentialDesignIntegrationTests(SportFrogDatabaseFixture fixture)
{
    private static OrganizationContext ActingIn(CompetitionTenant tenant)
    {
        var organization = new OrganizationContext();
        organization.Establish(tenant.OrgId, MembershipRole.Operator, Guid.NewGuid(), ipAddress: null);

        return organization;
    }

    private static CredentialDesignContract Design(
        string name,
        bool isDefault = false,
        string? legalText = null,
        string? backgroundKey = null,
        string? accent = null,
        string? logoKey = null) =>
        new(name, legalText, backgroundKey, accent, logoKey, isDefault);

    private static Task<IResult> Create(
        CompetitionTenant tenant, CompetitionTenant.Scope scope, CredentialDesignContract contract) =>
        CreateCredentialDesign.HandleAsync(
            contract, scope.Context, ActingIn(tenant), null!, CancellationToken.None);

    /// <summary>
    /// A competition with one accreditation category, which is what a batch
    /// refuses to print without, and the design it chose (or none).
    /// </summary>
    private static async Task<Competition> PrintableCompetitionAsync(
        CompetitionTenant tenant, CompetitionTenant.Scope scope, Guid? designId, PublicSettings? portal = null)
    {
        var competition = tenant.NewCompetition($"Copa {Guid.NewGuid():N}");
        competition.CredentialDesignId = designId;
        competition.Settings = new CompetitionSettings { Public = portal };

        scope.Context.Competitions.Add(competition);
        await scope.Context.SaveChangesAsync();

        scope.Context.AccreditationCategories.Add(new AccreditationCategory
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            CompetitionId = competition.Id,
            Code = "Aa",
            Name = "Deportista",
        });
        await scope.Context.SaveChangesAsync();

        return competition;
    }

    private static async Task<CredentialDesign> SeedDesignAsync(
        CompetitionTenant tenant, CompetitionTenant.Scope scope, string name, bool isDefault, string? legalText = null,
        string? backgroundKey = null, string? accent = null, string? logoKey = null)
    {
        var design = new CredentialDesign
        {
            Id = Guid.NewGuid(),
            OrgId = tenant.OrgId,
            Name = name,
            IsDefault = isDefault,
            LegalText = legalText,
            BackgroundKey = backgroundKey,
            AccentColorHex = accent,
            LogoKey = logoKey,
        };

        scope.Context.CredentialDesigns.Add(design);
        await scope.Context.SaveChangesAsync();

        return design;
    }

    [Fact]
    public async Task TheFirstDesignOfAnOrganization_BecomesTheDefault()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var result = await Create(tenant, scope, Design("Liga"));

        var created = Assert.IsType<Created<CreateCredentialDesign.Response>>(result);
        Assert.True(created.Value!.IsDefault);
    }

    [Fact]
    public async Task ASecondDesign_IsNotTheDefault_UnlessAskedFor()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        await Create(tenant, scope, Design("Primera"));

        var result = await Create(tenant, scope, Design("Segunda"));

        var created = Assert.IsType<Created<CreateCredentialDesign.Response>>(result);
        Assert.False(created.Value!.IsDefault);
    }

    [Fact]
    public async Task MakingAnotherDesignDefault_ClearsThePreviousOne()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        await Create(tenant, scope, Design("Primera"));
        await Create(tenant, scope, Design("Nueva default", isDefault: true));

        var defaults = await scope.Context.CredentialDesigns
            .Where(design => design.IsDefault)
            .Select(design => design.Name)
            .ToListAsync();

        Assert.Equal(["Nueva default"], defaults);
    }

    [Fact]
    public async Task ABatch_FreezesTheValuesOfTheDesignTheCompetitionChose()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        await SeedDesignAsync(tenant, scope, "Default", isDefault: true, legalText: "Aviso default.");
        var chosen = await SeedDesignAsync(
            tenant, scope, "Elegida", isDefault: false,
            legalText: "Aviso de la elegida.", backgroundKey: "org/k/fondo", accent: "#abcdef", logoKey: "org/k/logo");
        var competition = await PrintableCompetitionAsync(tenant, scope, chosen.Id);

        var result = await RequestDocumentBatch.BuildSnapshotAsync(scope.Context, competition, CancellationToken.None);

        var snapshot = Assert.IsType<CredentialSnapshot>(result.Snapshot);
        Assert.Equal("Aviso de la elegida.", snapshot.LegalText);
        Assert.Equal("org/k/fondo", snapshot.BackgroundKey);
        Assert.Equal("#abcdef", snapshot.AccentColorHex);
        Assert.Equal("org/k/logo", snapshot.CompetitionLogoKey);
    }

    [Fact]
    public async Task ACompetitionWithoutAChoice_PrintsFromTheOrganizationsDefault()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        await SeedDesignAsync(tenant, scope, "Otra", isDefault: false, legalText: "No debe usarse.");
        await SeedDesignAsync(tenant, scope, "Default", isDefault: true, legalText: "Aviso de la default.");
        var competition = await PrintableCompetitionAsync(tenant, scope, designId: null);

        var result = await RequestDocumentBatch.BuildSnapshotAsync(scope.Context, competition, CancellationToken.None);

        var snapshot = Assert.IsType<CredentialSnapshot>(result.Snapshot);
        Assert.Equal("Aviso de la default.", snapshot.LegalText);
    }

    [Fact]
    public async Task TheCompetitionsOwnLogo_WinsOverTheDesignsFallback()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var design = await SeedDesignAsync(tenant, scope, "Default", isDefault: true, logoKey: "org/k/logo-de-la-organizacion");
        var competition = await PrintableCompetitionAsync(
            tenant, scope, design.Id, new PublicSettings { LogoKey = "org/k/logo-de-la-competencia" });

        var result = await RequestDocumentBatch.BuildSnapshotAsync(scope.Context, competition, CancellationToken.None);

        var snapshot = Assert.IsType<CredentialSnapshot>(result.Snapshot);
        Assert.Equal("org/k/logo-de-la-competencia", snapshot.CompetitionLogoKey);
    }

    [Fact]
    public async Task AnOrganizationWithNoDesign_PrintsTheDecreeDefaultNotice()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);
        var competition = await PrintableCompetitionAsync(tenant, scope, designId: null);

        var result = await RequestDocumentBatch.BuildSnapshotAsync(scope.Context, competition, CancellationToken.None);

        var snapshot = Assert.IsType<CredentialSnapshot>(result.Snapshot);
        Assert.Equal(CredentialDesignResolver.DefaultLegalText, snapshot.LegalText);
        Assert.Null(snapshot.BackgroundKey);
    }

    [Fact]
    public async Task AChoiceOfADesignThatDoesNotExist_IsRefused()
    {
        var tenant = await CompetitionTenant.SeedAsync(fixture);
        await using var scope = await tenant.OpenAsync(fixture);

        var refusal = await CredentialDesignChoice.RefuseUnknownAsync(Guid.NewGuid(), scope.Context, CancellationToken.None);

        Assert.NotNull(refusal);
    }
}
