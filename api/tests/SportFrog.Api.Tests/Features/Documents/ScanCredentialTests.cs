using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Discipline;
using SportFrog.Domain.Documents;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Documents;

/// <summary>
/// The authenticated counterpart to <c>Features.Public.VerifyDocumentTests</c>:
/// what a steward's own session reads back for the same card, with the
/// photograph, the revocation reason and the sanction's own detail the
/// public endpoint is built never to say.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class ScanCredentialTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(Guid OrgId, Guid UserId, Guid CompetitionId, Guid TeamId, Guid AthleteId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = $"org-{orgId:N}" });
        setup.Users.Add(new User
        {
            Id = userId, Email = $"user-{userId:N}@example.com", PasswordHash = "hash", FullName = "Operador",
        });
        await setup.SaveChangesAsync();

        await using var transaction = await setup.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(setup, orgId);

        setup.Clubs.Add(new Club { Id = clubId, OrgId = orgId, Name = "Club" });

        setup.Rulesets.Add(new Ruleset
        {
            Id = rulesetId,
            OrgId = orgId,
            SportCode = Football,
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
            SportCode = Football,
            RulesetId = rulesetId,
            Name = "Competencia",
            Slug = $"comp-{competitionId:N}",
            Season = "2026",
            Format = "league",
            Settings = new CompetitionSettings(),
        });

        setup.Categories.Add(new Category { Id = categoryId, OrgId = orgId, CompetitionId = competitionId, Name = "Categoria" });
        setup.Teams.Add(new Team { Id = teamId, OrgId = orgId, ClubId = clubId, CategoryId = categoryId, Name = "Equipo" });

        setup.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Jugador",
            LastName = "DePrueba",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2000, 1, 1),

            // Null on purpose: AthletePhoto.LinkAsync answers null for an
            // absent key without ever touching object storage, so this
            // needs no real MinIO connection — same reasoning
            // TuitionPaymentsTests' own UnreachableReceipts follows.
            PhotoKey = null,
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, userId, competitionId, teamId, athleteId);
    }

    private async Task<string> SeedDocumentAsync(
        Fixture fx,
        bool teamOnly = false,
        DocumentState status = DocumentState.Issued,
        DateTimeOffset? revokedAt = null,
        string? revocationReason = null)
    {
        var serial = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.IssuedDocuments.Add(new IssuedDocument
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            Kind = DocumentKind.Credential,
            CompetitionId = fx.CompetitionId,
            AthleteId = teamOnly ? null : fx.AthleteId,
            TeamId = fx.TeamId,
            SerialNumber = serial,
            VisibleId = "IND-0001",
            Status = status,
            RevokedAt = revokedAt,
            RevocationReason = revocationReason,
            IssuedBy = fx.UserId,
            IssuedAt = Clock.GetUtcNow(),
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return serial;
    }

    private async Task SeedSanctionAsync(
        Fixture fx,
        SanctionCompletionRule rule = SanctionCompletionRule.Matches,
        int? matchesRequired = 3,
        decimal? fineAmount = null,
        bool voided = false,
        bool lifted = false,
        bool credentialWithheld = false)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Sanctions.Add(new Sanction
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            CompetitionId = fx.CompetitionId,
            AthleteId = fx.AthleteId,
            TeamId = fx.TeamId,
            Reason = "Falta de prueba",
            CompletionRule = rule,
            MatchesRequired = matchesRequired,
            FineAmount = fineAmount,
            VoidedAt = voided ? Clock.GetUtcNow() : null,
            VoidReason = voided ? "Anulación de prueba" : null,
            LiftedAt = lifted ? Clock.GetUtcNow() : null,
            LiftReason = lifted ? "Apelación de prueba" : null,
            CredentialWithheldAt = credentialWithheld ? Clock.GetUtcNow() : null,
            ImposedBy = fx.UserId,
            ImposedAt = Clock.GetUtcNow(),
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<IResult> ScanAsync(Fixture fx, string serial)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var photos = new SportFrog.Api.Features.Athletes.AthletePhoto(
            null!, new SportFrog.Api.Infrastructure.Tenancy.OrganizationContext(),
            null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<SportFrog.Api.Features.Athletes.AthletePhoto>.Instance);

        var activeSanctions = new ActiveSanctions(
            context, new SanctionProgress(context), new SanctionFinePayments(context));

        return await ScanCredential.HandleAsync(serial, context, activeSanctions, photos, Clock, CancellationToken.None);
    }

    [Fact]
    public async Task Scan_WithNoSanction_IsValidAndBlocksNobody()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx);

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.DocumentStatus.Should().Be(VerificationState.Valid);
        response.EntryBlocked.Should().BeFalse();
        response.ActiveSanctions.Should().BeEmpty();
        response.HolderFirstName.Should().Be("Jugador");
    }

    [Fact]
    public async Task Scan_WithAnUnresolvedSanction_IsBlockedAndNamesIt()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx);
        await SeedSanctionAsync(fx, matchesRequired: 3, credentialWithheld: true);

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.EntryBlocked.Should().BeTrue();
        var sanction = response.ActiveSanctions.Should().ContainSingle().Subject;
        sanction.Reason.Should().Be("Falta de prueba");
        sanction.MatchesRemaining.Should().Be(3);
        sanction.CredentialWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task Scan_WithAVoidedSanction_ExcludesItAndIsNotBlocked()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx);
        await SeedSanctionAsync(fx, voided: true);

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.EntryBlocked.Should().BeFalse();
        response.ActiveSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_WithALiftedSanction_ExcludesItAndIsNotBlocked()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx);
        await SeedSanctionAsync(fx, lifted: true);

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.EntryBlocked.Should().BeFalse();
        response.ActiveSanctions.Should().BeEmpty();
    }

    /// <summary>
    /// Unlike the public endpoint, this one is allowed to say why — see
    /// <see cref="ScanCredential.Response.RevocationReason"/>'s own remarks.
    /// </summary>
    [Fact]
    public async Task Scan_ARevokedDocument_NamesWhy()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(
            fx, status: DocumentState.Revoked, revokedAt: Clock.GetUtcNow(), revocationReason: "Tarjeta extraviada");

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.DocumentStatus.Should().Be(VerificationState.Revoked);
        response.RevocationReason.Should().Be("Tarjeta extraviada");
        response.EntryBlocked.Should().BeTrue();
    }

    /// <summary>
    /// A document issued to a team names nobody a sanction could name — see
    /// <see cref="Sanction"/>'s own remarks on why a sanction is always
    /// against a person. This must read as "nothing to check", not throw.
    /// </summary>
    [Fact]
    public async Task Scan_ADocumentIssuedToATeam_NamesNoHolderAndChecksNoSanction()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx, teamOnly: true);

        var result = await ScanAsync(fx, serial);

        var response = Assert.IsType<Ok<ScanCredential.Response>>(result).Value!;
        response.HolderFirstName.Should().BeNull();
        response.ActiveSanctions.Should().BeEmpty();
        response.EntryBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Scan_ASerialTypedWithHyphensAndLowercase_StillResolves()
    {
        var fx = await SeedAsync();
        var serial = await SeedDocumentAsync(fx);
        var typed = string.Join("-", serial.Chunk(4).Select(chunk => new string(chunk))).ToLowerInvariant();

        var result = await ScanAsync(fx, typed);

        Assert.IsType<Ok<ScanCredential.Response>>(result);
    }

    [Fact]
    public async Task Scan_AnUnknownSerial_ReturnsNotFound()
    {
        var fx = await SeedAsync();
        await SeedDocumentAsync(fx);

        var result = await ScanAsync(fx, "ZZZZZZZZZZZZ");

        Assert.IsType<NotFound>(result);
    }
}
