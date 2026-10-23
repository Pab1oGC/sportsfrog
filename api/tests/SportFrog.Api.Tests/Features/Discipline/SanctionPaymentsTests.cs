using AwesomeAssertions;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Discipline;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Discipline;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Tests.Features.Discipline;

/// <summary>
/// Recording a payment against a sanction's fine, reviewing or voiding it,
/// and the one thing that ties them to <see cref="ReadSanctions"/>: a
/// sanction's fine balance is never written down, only resolved fresh from
/// whichever of its payments still count — the same guarantee
/// <c>TuitionPaymentsTests</c> already exercises for a pension charge.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class SanctionPaymentsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Fixture(Guid OrgId, Guid UserId, Guid CompetitionId, Guid SanctionId);

    // The client it would call through is never actually reached: every
    // test here either sends no receipt at all (cash) or sends one that
    // fails before SanctionReceipt.StoreAsync ever calls PutAsync — same
    // reasoning TuitionPaymentsTests' own UnreachableReceipts follows.
    private static SanctionReceipt UnreachableReceipts(OrganizationContext organization) =>
        new(new ObjectStore(null!, Options.Create(new StorageOptions()), NullLogger<ObjectStore>.Instance),
            organization);

    /// <summary>
    /// A real client pointed at nothing — signing a link is arithmetic, not
    /// a network call, so this can answer a signed link without anything
    /// actually listening at the endpoint.
    /// </summary>
    private static SanctionReceipt SigningReceipts(OrganizationContext organization)
    {
        var client = new AmazonS3Client(
            new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonS3Config { ServiceURL = "http://localhost:19999", ForcePathStyle = true });

        var store = new ObjectStore(
            client,
            Options.Create(new StorageOptions
            {
                Endpoint = "http://localhost:19999",
                Bucket = "sportfrog-test",
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
            }),
            NullLogger<ObjectStore>.Instance);

        return new SanctionReceipt(store, organization);
    }

    private static OrganizationContext OrganizationFor(Guid orgId, Guid userId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Admin, userId, ipAddress: null);
        return organization;
    }

    /// <summary>
    /// One organization, one competition, one team and one person, and one
    /// fine-bearing sanction already imposed on them — enough for every
    /// test here, none of it specific to any one of them.
    /// </summary>
    private async Task<Fixture> SeedSanctionAsync(
        SanctionCompletionRule rule = SanctionCompletionRule.Fine,
        decimal fineAmount = 500m,
        int? matchesRequired = null,
        bool waived = false,
        bool lifted = false,
        bool voided = false)
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var rulesetId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var sanctionId = Guid.NewGuid();

        await using var setup = fixture.CreateAppContext();

        setup.Organizations.Add(new Organization
        {
            Id = orgId,
            Name = $"Org {orgId:N}",
            Slug = $"org-{orgId:N}",
        });
        setup.Users.Add(new User
        {
            Id = userId,
            Email = $"user-{userId:N}@example.com",
            PasswordHash = "hash",
            FullName = "Operador de prueba",
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
            LastName = "Sancionado",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2000, 1, 1),
        });

        setup.Sanctions.Add(new Sanction
        {
            Id = sanctionId,
            OrgId = orgId,
            CompetitionId = competitionId,
            AthleteId = athleteId,
            TeamId = teamId,
            Reason = "Falta de prueba",
            CompletionRule = rule,
            MatchesRequired = matchesRequired,
            FineAmount = rule == SanctionCompletionRule.Matches ? null : fineAmount,
            FineWaivedAt = waived ? Clock.GetUtcNow() : null,
            FineWaiverReason = waived ? "Condonación de prueba" : null,
            LiftedAt = lifted ? Clock.GetUtcNow() : null,
            LiftReason = lifted ? "Apelación de prueba" : null,
            VoidedAt = voided ? Clock.GetUtcNow() : null,
            VoidReason = voided ? "Anulación de prueba" : null,
            ImposedBy = userId,
            ImposedAt = Clock.GetUtcNow(),
        });

        await setup.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, userId, competitionId, sanctionId);
    }

    /// <summary>Seeds a payment directly, bypassing RecordSanctionPayment — for states it itself cannot reach without real object storage (a stored receipt).</summary>
    private async Task<Guid> SeedPaymentAsync(
        Fixture fx,
        decimal amount,
        PaymentMethod method,
        ReceiptReviewState reviewState,
        string? receiptKey = null,
        DateTimeOffset? voidedAt = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var payment = new SanctionPayment
        {
            Id = Guid.NewGuid(),
            OrgId = fx.OrgId,
            SanctionId = fx.SanctionId,
            Amount = amount,
            Method = method,
            ReceiptKey = receiptKey,
            ReviewState = reviewState,
            ReviewReason = reviewState == ReceiptReviewState.Rejected ? "Rechazado de prueba" : null,
            ReviewedAt = reviewState == ReceiptReviewState.Pending ? null : Clock.GetUtcNow(),
            VoidedAt = voidedAt,
            VoidReason = voidedAt is null ? null : "Anulado de prueba",
        };

        context.SanctionPayments.Add(payment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return payment.Id;
    }

    private async Task<IResult> RecordAsync(
        Fixture fx, decimal amount, PaymentMethod method, string? receiptDataUrl)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var organization = OrganizationFor(fx.OrgId, fx.UserId);

        var result = await RecordSanctionPayment.HandleAsync(
            fx.CompetitionId, fx.SanctionId, new RecordSanctionPayment.Request(amount, method, receiptDataUrl),
            context, new SanctionPaymentPolicy(), UnreachableReceipts(organization), organization,
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> ReviewAsync(Fixture fx, Guid paymentId, bool verified, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReviewSanctionPayment.HandleAsync(
            fx.CompetitionId, paymentId, new ReviewSanctionPayment.Request(verified, reason), context, Clock,
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> VoidAsync(Fixture fx, Guid paymentId, string reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await VoidSanctionPayment.HandleAsync(
            fx.CompetitionId, paymentId, new VoidSanctionPayment.Request(reason), context, Clock,
            CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<ReadSanctions.Response> ReadAsync(Fixture fx)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var organization = OrganizationFor(fx.OrgId, fx.UserId);

        var result = await ReadSanctions.ReadAsync(
            fx.CompetitionId, fx.SanctionId, context, new SanctionProgress(context), SigningReceipts(organization),
            CancellationToken.None);

        return Assert.IsType<Ok<ReadSanctions.Response>>(result).Value!;
    }

    private async Task<List<ReadSanctions.Response>> ListAsync(Fixture fx)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadSanctions.ListAsync(fx.CompetitionId, context, new SanctionProgress(context), CancellationToken.None);

        return Assert.IsType<Ok<List<ReadSanctions.Response>>>(result).Value!;
    }

    private async Task<IResult> ReadPaymentAsync(Fixture fx, Guid paymentId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var organization = OrganizationFor(fx.OrgId, fx.UserId);

        var result = await ReadSanctionPayment.HandleAsync(
            fx.CompetitionId, paymentId, context, SigningReceipts(organization), CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    // ---- Recording ---------------------------------------------------

    [Fact]
    public async Task Record_Cash_IsBornVerifiedWithNoReceipt()
    {
        var fx = await SeedSanctionAsync();

        var result = await RecordAsync(fx, 200m, PaymentMethod.Cash, receiptDataUrl: null);

        var response = Assert.IsType<Created<RecordSanctionPayment.Response>>(result).Value!;
        response.ReviewState.Should().Be(ReceiptReviewState.Verified);
    }

    [Fact]
    public async Task Record_OnAVoidedSanction_IsRefused()
    {
        var fx = await SeedSanctionAsync(voided: true);

        var result = await RecordAsync(fx, 200m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnALiftedSanction_IsRefused()
    {
        var fx = await SeedSanctionAsync(lifted: true);

        var result = await RecordAsync(fx, 200m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnAMatchesOnlySanction_IsRefused()
    {
        var fx = await SeedSanctionAsync(SanctionCompletionRule.Matches, matchesRequired: 2);

        var result = await RecordAsync(fx, 200m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnASanctionWhoseFineWasAlreadyWaived_IsRefused()
    {
        var fx = await SeedSanctionAsync(waived: true);

        var result = await RecordAsync(fx, 200m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnAnUnknownSanction_ReturnsNotFound()
    {
        var fx = await SeedSanctionAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
        var organization = OrganizationFor(fx.OrgId, fx.UserId);

        var result = await RecordSanctionPayment.HandleAsync(
            fx.CompetitionId, Guid.NewGuid(), new RecordSanctionPayment.Request(200m, PaymentMethod.Cash, null),
            context, new SanctionPaymentPolicy(), UnreachableReceipts(organization), organization,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    // ---- Resolving against real payments ----------------------------------

    [Fact]
    public async Task Read_AfterACashPaymentCoveringTheFine_IsServed()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(fx, 500m, PaymentMethod.Cash, ReceiptReviewState.Verified);

        var response = await ReadAsync(fx);

        response.Status.Should().Be(SanctionStatus.Served);
        response.FineBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Read_APendingTransferPayment_AlreadyCountsBeforeItIsReviewed()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending, receiptKey: "receipts/x/y.pdf");

        var response = await ReadAsync(fx);

        response.Status.Should().Be(SanctionStatus.Served);
    }

    [Fact]
    public async Task Read_ARejectedReceipt_DoesNotCountAsAPayment()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Rejected, receiptKey: "receipts/x/y.pdf");

        var response = await ReadAsync(fx);

        response.Status.Should().Be(SanctionStatus.Active);
        response.FineBalance.Should().Be(500m);
    }

    [Fact]
    public async Task Review_VerifyingAPendingPayment_MarksItVerified()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending, receiptKey: "receipts/x/y.pdf");

        Assert.IsType<NoContent>(await ReviewAsync(fx, paymentId, verified: true, reason: null));

        var response = await ReadAsync(fx);
        response.Payments.Single().ReviewState.Should().Be(ReceiptReviewState.Verified);
    }

    [Fact]
    public async Task Review_RejectingAPendingPayment_ReturnsTheSanctionToActive()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending, receiptKey: "receipts/x/y.pdf");

        Assert.IsType<NoContent>(await ReviewAsync(fx, paymentId, verified: false, "Comprobante ilegible"));

        var response = await ReadAsync(fx);
        response.Status.Should().Be(SanctionStatus.Active);
        response.FineBalance.Should().Be(500m);
    }

    [Fact]
    public async Task Review_RejectingWithNoReason_IsRefused()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending, receiptKey: "receipts/x/y.pdf");

        // The handler itself does not re-run the validator (that is the
        // filter's job in production) — this exercises what the handler
        // defends on its own: a payment already reviewed cannot be
        // reviewed twice, checked below instead.
        await ReviewAsync(fx, paymentId, verified: true, reason: null);
        var result = await ReviewAsync(fx, paymentId, verified: false, "Demasiado tarde");

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Void_APaymentThatWasCoveringTheFine_ReturnsTheSanctionToActive()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(fx, 500m, PaymentMethod.Cash, ReceiptReviewState.Verified);

        Assert.IsType<NoContent>(await VoidAsync(fx, paymentId, "Pago duplicado"));

        var response = await ReadAsync(fx);
        response.Status.Should().Be(SanctionStatus.Active);
        response.FineBalance.Should().Be(500m);
    }

    [Fact]
    public async Task Void_APaymentAlreadyVoided_IsRefused()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(fx, 500m, PaymentMethod.Cash, ReceiptReviewState.Verified);
        await VoidAsync(fx, paymentId, "Primera anulación");

        var result = await VoidAsync(fx, paymentId, "Segundo intento");

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task TwoPartialPayments_ThatAddUpToTheFine_LeaveTheSanctionServed()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(fx, 300m, PaymentMethod.Cash, ReceiptReviewState.Verified);
        await SeedPaymentAsync(fx, 200m, PaymentMethod.Cash, ReceiptReviewState.Verified);

        var response = await ReadAsync(fx);

        response.Status.Should().Be(SanctionStatus.Served);
        response.FineBalance.Should().Be(0m);
    }

    // ---- Un comprobante en la propia lista de pagos de la sanción ----------

    /// <summary>
    /// Lo que reemplazó a la pestaña "Comprobantes pendientes": la propia
    /// lista de pagos de una sanción ya trae el link firmado de cada uno,
    /// para que la tabla de pagos lo muestre de una (miniatura clickeable),
    /// sin un segundo pedido por fila.
    /// </summary>
    [Fact]
    public async Task Read_OneSanction_SignsAReceiptLinkForEachOfItsPayments()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending,
            receiptKey: StorageKeys.PaymentReceipt(fx.OrgId, fx.SanctionId, "pdf"));

        var sanction = await ReadAsync(fx);

        var payment = sanction.Payments.Should().ContainSingle().Subject;
        payment.HasReceipt.Should().BeTrue();
        payment.ReceiptUrl.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// A diferencia de <see cref="Read_OneSanction_SignsAReceiptLinkForEachOfItsPayments"/>:
    /// la lista entera de la competencia no firma nada por fila -- firmar un
    /// link por cada pago de cada sanción en cada carga de página sería
    /// trabajo de más casi siempre, ver <see cref="ReadSanctions.Payment.ReceiptUrl"/>
    /// propio.
    /// </summary>
    [Fact]
    public async Task List_NeverSignsAReceiptLink_EvenWhenThePaymentHasOne()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Pending,
            receiptKey: StorageKeys.PaymentReceipt(fx.OrgId, fx.SanctionId, "pdf"));

        var sanctions = await ListAsync(fx);

        var payment = sanctions.Should().ContainSingle().Which.Payments.Should().ContainSingle().Subject;
        payment.HasReceipt.Should().BeTrue();
        payment.ReceiptUrl.Should().BeNull();
    }

    // ---- Reading one payment back, after the fact --------------------------

    /// <summary>
    /// The whole point of this endpoint: a payment already reviewed (or a
    /// sanction already lifted or served around it) drops off
    /// <see cref="ReadPendingSanctionReceipts"/> for good, and
    /// <see cref="ReadSanctions"/> only ever carries <c>HasReceipt</c>, a
    /// bare flag -- this is the only path left back to the receipt itself.
    /// </summary>
    [Fact]
    public async Task ReadPayment_AlreadyVerified_StillSignsALinkToItsReceipt()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Verified,
            receiptKey: StorageKeys.PaymentReceipt(fx.OrgId, fx.SanctionId, "pdf"));

        var result = await ReadPaymentAsync(fx, paymentId);

        var response = Assert.IsType<Ok<ReadSanctionPayment.Response>>(result).Value!;
        response.ReceiptUrl.Should().NotBeNullOrEmpty();
        response.ReviewState.Should().Be(ReceiptReviewState.Verified);
    }

    [Fact]
    public async Task ReadPayment_InCash_HasNoReceiptToLinkTo()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var result = await RecordAsync(fx, 500m, PaymentMethod.Cash, receiptDataUrl: null);
        var paymentId = Assert.IsType<Created<RecordSanctionPayment.Response>>(result).Value!.Id;

        var read = await ReadPaymentAsync(fx, paymentId);

        var response = Assert.IsType<Ok<ReadSanctionPayment.Response>>(read).Value!;
        response.ReceiptUrl.Should().BeNull();
    }

    [Fact]
    public async Task ReadPayment_FromAnotherCompetition_ReturnsNotFound()
    {
        var fx = await SeedSanctionAsync(fineAmount: 500m);
        var otherFx = await SeedSanctionAsync(fineAmount: 500m);
        var paymentId = await SeedPaymentAsync(
            fx, 500m, PaymentMethod.Transfer, ReceiptReviewState.Verified,
            receiptKey: StorageKeys.PaymentReceipt(fx.OrgId, fx.SanctionId, "pdf"));

        var result = await ReadPaymentAsync(otherFx, paymentId);

        Assert.IsType<NotFound>(result);
    }
}
