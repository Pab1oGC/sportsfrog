using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Public;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Discipline;
using SportFrog.Domain.Documents;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Public;

/// <summary>
/// The QR code's own answer, read exactly as <c>sportfrog_public</c> reads
/// it — through a real <see cref="PublicDocumentReader"/> built over the
/// same keyed connection string Program.cs wires it to, not a role this
/// test only pretends to hold.
/// </summary>
/// <remarks>
/// <see cref="VerificationState.Blocked"/> is the one answer this file is
/// really here to prove: that <c>sanction_blocks_entry</c> — the one thing
/// sportfrog_public is granted on the whole discipline module — actually
/// says the right thing, and that it is asked only when asking could change
/// the answer at all.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class VerifyDocumentTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(
        Guid OrgId, string OrgSlug, Guid UserId, Guid CompetitionId, Guid TeamId, Guid AthleteId);

    private async Task<Fixture> SeedAsync()
    {
        var orgId = Guid.NewGuid();
        var orgSlug = $"org-{orgId:N}";
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization { Id = orgId, Name = $"Org {orgId:N}", Slug = orgSlug });
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
            IsPublic = true,
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
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, orgSlug, userId, competitionId, teamId, athleteId);
    }

    private async Task<string> SeedCredentialAsync(
        Fixture fx, DocumentState status = DocumentState.Issued, DateTimeOffset? revokedAt = null)
    {
        var serial = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.IssuedDocuments.Add(new IssuedDocument
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            TemplateId = null,
            TemplateVersion = null,
            Kind = DocumentKind.Credential,
            CompetitionId = fx.CompetitionId,
            AthleteId = fx.AthleteId,
            TeamId = fx.TeamId,
            SerialNumber = serial,
            VisibleId = "IND-0001",
            Status = status,
            RevokedAt = revokedAt,
            RevocationReason = revokedAt is null ? null : "Tarjeta extraviada",
            IssuedBy = fx.UserId,
            IssuedAt = Clock.GetUtcNow(),
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return serial;
    }

    private async Task<Guid> SeedSanctionAsync(
        Fixture fx,
        SanctionCompletionRule rule = SanctionCompletionRule.Matches,
        int? matchesRequired = 3,
        decimal? fineAmount = null,
        bool voided = false,
        bool lifted = false,
        bool credentialWithheld = false)
    {
        var sanctionId = Guid.NewGuid();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        context.Sanctions.Add(new Sanction
        {
            Id = sanctionId,
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

        return sanctionId;
    }

    private async Task<IResult> VerifyAsync(Fixture fx, string serial)
    {
        var dataSource = SportFrogDataSource.Create(fixture.PublicConnectionString);
        try
        {
            var reader = new PublicDocumentReader(dataSource);

            return await VerifyDocument.HandleAsync(fx.OrgSlug, serial, reader, Clock, CancellationToken.None);
        }
        finally
        {
            await dataSource.DisposeAsync();
        }
    }

    [Fact]
    public async Task Verify_WithNoSanction_IsValid()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Valid);
        response.ActiveSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_WithAnUnresolvedSanction_IsBlocked()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        await SeedSanctionAsync(fx);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Blocked);
    }

    /// <summary>
    /// What <see cref="VerifyDocument.Response.ActiveSanctions"/> is for:
    /// anybody holding the card now learns it is a suspension by matches,
    /// how many remain, and whether the credential itself is withheld --
    /// never the <see cref="Sanction.Reason"/> behind it (see
    /// <see cref="VerifyDocument.PublicActiveSanction"/>'s own remarks,
    /// and <see cref="SportFrog.Api.Tests.Features.Documents.ScanCredentialTests"/>
    /// for the authenticated counterpart that still says why).
    /// </summary>
    [Fact]
    public async Task Verify_WithAnUnresolvedMatchesSanction_NamesTheKindNeverTheReason()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        await SeedSanctionAsync(fx, matchesRequired: 3, credentialWithheld: true);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Blocked);
        var sanction = response.ActiveSanctions.Should().ContainSingle().Subject;
        sanction.CompletionRule.Should().Be(SanctionCompletionRule.Matches);
        sanction.MatchesRemaining.Should().Be(3);
        sanction.FineBalance.Should().BeNull();
        sanction.CredentialWithheld.Should().BeTrue();
    }

    /// <summary>
    /// The other half of <see cref="SanctionCompletionRule"/> this endpoint
    /// now names -- a balance still owed, with no matches term to speak of.
    /// </summary>
    [Fact]
    public async Task Verify_WithAnUnresolvedFineSanction_NamesTheBalance()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        await SeedSanctionAsync(fx, SanctionCompletionRule.Fine, matchesRequired: null, fineAmount: 500m);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Blocked);
        var sanction = response.ActiveSanctions.Should().ContainSingle().Subject;
        sanction.CompletionRule.Should().Be(SanctionCompletionRule.Fine);
        sanction.MatchesRemaining.Should().BeNull();
        sanction.FineBalance.Should().Be(500m);
    }

    [Fact]
    public async Task Verify_WithAVoidedSanction_StaysValid()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        await SeedSanctionAsync(fx, voided: true);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Valid);
        response.ActiveSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_WithALiftedSanction_StaysValid()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        await SeedSanctionAsync(fx, lifted: true);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Valid);
        response.ActiveSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_WithAFineFullyWaived_StaysValid()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx);
        var sanctionId = await SeedSanctionAsync(fx, SanctionCompletionRule.Fine, matchesRequired: null, fineAmount: 500m);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
        var sanction = await context.Sanctions.SingleAsync(s => s.Id == sanctionId);
        sanction.FineWaivedAt = Clock.GetUtcNow();
        sanction.FineWaiverReason = "Condonada de prueba";
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Valid);
        response.ActiveSanctions.Should().BeEmpty();
    }

    /// <summary>
    /// Revocation outranks a sanction the same way it outranks the dates —
    /// the document's own remarks already draw that line, and a blocked
    /// person's card revoked for an unrelated reason must not quietly read
    /// as merely blocked once it is given back.
    /// </summary>
    [Fact]
    public async Task Verify_ARevokedDocument_StaysRevokedEvenWithAnUnresolvedSanction()
    {
        var fx = await SeedAsync();
        var serial = await SeedCredentialAsync(fx, DocumentState.Revoked, revokedAt: Clock.GetUtcNow());
        await SeedSanctionAsync(fx);

        var result = await VerifyAsync(fx, serial);

        var response = Assert.IsType<Ok<VerifyDocument.Response>>(result).Value!;
        response.Status.Should().Be(VerificationState.Revoked);
        response.ActiveSanctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_AnUnknownSerial_ReturnsNotFound()
    {
        var fx = await SeedAsync();
        await SeedCredentialAsync(fx);

        var result = await VerifyAsync(fx, "ZZZZ-ZZZZ-ZZZZ");

        Assert.IsType<NotFound>(result);
    }
}
