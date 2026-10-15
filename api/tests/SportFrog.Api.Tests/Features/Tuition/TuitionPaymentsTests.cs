using AwesomeAssertions;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SportFrog.Api.Features.Tuition;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Tests.Infrastructure.Persistence;
using SportFrog.Domain.School;

namespace SportFrog.Api.Tests.Features.Tuition;

/// <summary>
/// Recording a payment against a charge, reviewing or voiding it, and the
/// one thing that ties them to <see cref="ReadTuitionAccount"/>: a charge's
/// own status is never written down, only resolved fresh from whichever of
/// its payments still count.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class TuitionPaymentsTests(SportFrogDatabaseFixture fixture)
{
    private const string Football = "football";

    private static readonly DateOnly Period = new(2026, 3, 1);
    private static readonly TimeProvider Clock = new FixedTimeProvider(
        new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // The client it would call through is never actually reached: every
    // test here either sends no receipt at all (cash) or sends one that
    // fails before PaymentReceipt.StoreAsync ever calls PutAsync — the same
    // reasoning ReportQueriesTests' own Store follows for ObjectStore.
    private static PaymentReceipt UnreachableReceipts(OrganizationContext organization) =>
        new(new ObjectStore(null!, Options.Create(new StorageOptions()), NullLogger<ObjectStore>.Instance),
            organization);

    /// <summary>
    /// A real client pointed at nothing — signing a link is arithmetic, not
    /// a network call (see <c>ObjectStore.ReadLinkAsync</c>'s own remarks),
    /// so this can answer a signed link without anything actually listening
    /// at the endpoint. Only safe for the read side: an actual PutAsync or
    /// GetObjectAsync through this would try to connect and fail.
    /// </summary>
    private static PaymentReceipt SigningReceipts(OrganizationContext organization)
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

        return new PaymentReceipt(store, organization);
    }

    private static OrganizationContext OrganizationFor(Guid orgId)
    {
        var organization = new OrganizationContext();
        organization.Establish(orgId, MembershipRole.Admin, userId: Guid.NewGuid(), ipAddress: null);
        return organization;
    }

    private async Task<Guid> SeedOrganizationAsync()
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

        return orgId;
    }

    private sealed record Fixture(Guid OrgId, Guid ChargeId);

    private async Task<Fixture> SeedChargeAsync(
        decimal amountBob = 100m, bool waived = false, bool voided = false)
    {
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var courseId = Guid.NewGuid();
        context.Courses.Add(new Course
        {
            Id = courseId,
            OrgId = orgId,
            SportCode = Football,
            Name = $"Curso {Guid.NewGuid():N}",
            MonthlyFeeBob = amountBob,
            StartsOn = new DateOnly(2026, 1, 1),
        });

        var athleteId = Guid.NewGuid();
        context.Athletes.Add(new Athlete
        {
            Id = athleteId,
            OrgId = orgId,
            FirstName = "Estudiante",
            LastName = "DePrueba",
            DocumentId = $"doc-{athleteId:N}",
            BirthDate = new DateOnly(2015, 1, 1),
        });

        var enrollmentId = Guid.NewGuid();
        context.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = enrollmentId,
            OrgId = orgId,
            CourseId = courseId,
            AthleteId = athleteId,
            EnrolledAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        var chargeId = Guid.NewGuid();
        context.TuitionCharges.Add(new TuitionCharge
        {
            Id = chargeId,
            OrgId = orgId,
            EnrollmentId = enrollmentId,
            Period = Period,
            AmountBob = amountBob,
            DueOn = Period.AddMonths(1).AddDays(-1),
            WaivedAt = waived ? Clock.GetUtcNow() : null,
            WaiverReason = waived ? "Beca de prueba" : null,
            VoidedAt = voided ? Clock.GetUtcNow() : null,
            VoidReason = voided ? "Error de prueba" : null,
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new Fixture(orgId, chargeId);
    }

    /// <summary>Seeds a payment directly, bypassing RecordPayment — for states RecordPayment itself cannot reach without real object storage (a stored receipt).</summary>
    private async Task<Guid> SeedPaymentAsync(
        Guid orgId,
        Guid chargeId,
        decimal amountBob,
        PaymentMethod method,
        ReceiptReviewState reviewState,
        string? receiptKey = null,
        DateTimeOffset? voidedAt = null)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var payment = new TuitionPayment
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ChargeId = chargeId,
            AmountBob = amountBob,
            Method = method,
            ReceiptKey = receiptKey,
            ReviewState = reviewState,
            ReviewReason = reviewState == ReceiptReviewState.Rejected ? "Rechazado de prueba" : null,
            ReviewedAt = reviewState == ReceiptReviewState.Pending ? null : Clock.GetUtcNow(),
            VoidedAt = voidedAt,
            VoidReason = voidedAt is null ? null : "Anulado de prueba",
        };

        context.TuitionPayments.Add(payment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return payment.Id;
    }

    private async Task<IResult> RecordAsync(
        Guid orgId, Guid chargeId, decimal amount, PaymentMethod method, string? receiptDataUrl)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var organization = OrganizationFor(orgId);

        var result = await RecordPayment.HandleAsync(
            chargeId, new RecordPayment.Request(amount, method, receiptDataUrl), context,
            new PaymentPolicy(), UnreachableReceipts(organization), organization, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> ReviewAsync(Guid orgId, Guid paymentId, bool verified, string? reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReviewPayment.HandleAsync(
            paymentId, new ReviewPayment.Request(verified, reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<IResult> VoidAsync(Guid orgId, Guid paymentId, string reason)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await VoidPayment.HandleAsync(
            paymentId, new VoidPayment.Request(reason), context, Clock, CancellationToken.None);
        await transaction.CommitAsync();

        return result;
    }

    private async Task<List<ReadTuitionAccount.Charge>> ReadAccountAsync(Guid orgId, Guid enrollmentId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadTuitionAccount.ListAsync(enrollmentId, context, CancellationToken.None);

        return Assert.IsType<Ok<List<ReadTuitionAccount.Charge>>>(result).Value!;
    }

    private async Task<Guid> EnrollmentIdFor(Guid orgId, Guid chargeId)
    {
        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        return await context.TuitionCharges.AsNoTracking()
            .Where(charge => charge.Id == chargeId)
            .Select(charge => charge.EnrollmentId)
            .SingleAsync();
    }

    [Fact]
    public async Task Record_Cash_IsBornVerifiedWithNoReceipt()
    {
        var fx = await SeedChargeAsync();

        var result = await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null);

        var response = Assert.IsType<Created<RecordPayment.Response>>(result).Value!;
        response.ReviewState.Should().Be(ReceiptReviewState.Verified);
    }

    // The three rules below run through FluentValidation's own Validate(),
    // not through RecordAsync/HandleAsync: ContractValidation.ValidateContracts
    // is an endpoint filter, part of the HTTP pipeline a direct HandleAsync
    // call never goes through -- calling HandleAsync with a request these
    // reject would reach the handler's own logic (or the database's own
    // ck_payment_receipt) instead, same as every other validator in this
    // codebase is proven at this level rather than through the endpoint.

    [Fact]
    public void Validator_CashWithAReceipt_IsRejected()
    {
        var result = new RecordPayment.Validator().Validate(
            new RecordPayment.Request(50m, PaymentMethod.Cash, "data:image/png;base64,QUJD"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TransferWithNoReceipt_IsRejected()
    {
        var result = new RecordPayment.Validator().Validate(
            new RecordPayment.Request(50m, PaymentMethod.Transfer, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_TransferWithAReceipt_IsAccepted()
    {
        var result = new RecordPayment.Validator().Validate(
            new RecordPayment.Request(50m, PaymentMethod.Transfer, "data:image/png;base64,QUJD"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_AmountNotPositive_IsRejected()
    {
        var result = new RecordPayment.Validator().Validate(
            new RecordPayment.Request(0m, PaymentMethod.Cash, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Record_TransferWithAnUnreadableReceipt_IsRefused_WithoutEverTouchingTheStore()
    {
        var fx = await SeedChargeAsync();

        // A syntactically fine data URL whose payload is not an image: it
        // passes InlineReceipt.IsAcceptable's shape check and fails only once
        // PaymentReceipt.StoreAsync actually tries to decode it -- before
        // ever reaching the store, which is why UnreachableReceipts is safe
        // here. This is the handler's own refusal, not the validator's.
        var garbage = Convert.ToBase64String("esto no es una imagen"u8.ToArray());

        var result = await RecordAsync(
            fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Transfer, $"data:image/png;base64,{garbage}");

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnAVoidedCharge_ReturnsConflict()
    {
        var fx = await SeedChargeAsync(voided: true);

        var result = await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnAWaivedCharge_ReturnsConflict()
    {
        var fx = await SeedChargeAsync(waived: true);

        var result = await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Record_OnAnUnknownCharge_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await RecordAsync(orgId, Guid.NewGuid(), 50m, PaymentMethod.Cash, receiptDataUrl: null);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task TwoPartialCashPayments_LeaveTheChargeInPaidStatus_WithNobodyWritingPaid()
    {
        var fx = await SeedChargeAsync(amountBob: 100m);

        await RecordAsync(fx.OrgId, fx.ChargeId, 60m, PaymentMethod.Cash, receiptDataUrl: null);
        await RecordAsync(fx.OrgId, fx.ChargeId, 40m, PaymentMethod.Cash, receiptDataUrl: null);

        var enrollmentId = await EnrollmentIdFor(fx.OrgId, fx.ChargeId);
        var charge = (await ReadAccountAsync(fx.OrgId, enrollmentId)).Single();

        charge.Status.Should().Be(ChargeStatus.Paid);
        charge.PaidBob.Should().Be(100m);
        charge.BalanceBob.Should().Be(0m);
    }

    [Fact]
    public async Task VoidingOnePayment_ReturnsTheChargeToPartialAutomatically()
    {
        var fx = await SeedChargeAsync(amountBob: 100m);

        await RecordAsync(fx.OrgId, fx.ChargeId, 60m, PaymentMethod.Cash, receiptDataUrl: null);
        var secondId = Assert.IsType<Created<RecordPayment.Response>>(
            await RecordAsync(fx.OrgId, fx.ChargeId, 40m, PaymentMethod.Cash, receiptDataUrl: null)).Value!.Id;

        var enrollmentId = await EnrollmentIdFor(fx.OrgId, fx.ChargeId);
        (await ReadAccountAsync(fx.OrgId, enrollmentId)).Single().Status.Should().Be(ChargeStatus.Paid);

        Assert.IsType<NoContent>(await VoidAsync(fx.OrgId, secondId, "Pago duplicado"));

        var charge = (await ReadAccountAsync(fx.OrgId, enrollmentId)).Single();
        charge.Status.Should().Be(ChargeStatus.Partial);
        charge.PaidBob.Should().Be(60m);
    }

    [Fact]
    public async Task ARejectedReceipt_DoesNotCountAsAPayment()
    {
        var fx = await SeedChargeAsync(amountBob: 100m);

        var paymentId = await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 100m, PaymentMethod.Transfer, ReceiptReviewState.Pending,
            receiptKey: "orgs/fake/receipts/fake/fake.jpg");

        Assert.IsType<NoContent>(await ReviewAsync(fx.OrgId, paymentId, verified: false, "Comprobante ilegible"));

        var enrollmentId = await EnrollmentIdFor(fx.OrgId, fx.ChargeId);
        var charge = (await ReadAccountAsync(fx.OrgId, enrollmentId)).Single();

        charge.Status.Should().Be(ChargeStatus.Pending);
        charge.PaidBob.Should().Be(0m);
    }

    [Fact]
    public async Task APendingReceipt_AlreadyCountsBeforeItIsReviewed()
    {
        // A rejection excludes a payment from the balance; simply not having
        // been reviewed yet does not -- see ReadTuitionAccount's own
        // remarks for why.
        var fx = await SeedChargeAsync(amountBob: 100m);

        await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 100m, PaymentMethod.Transfer, ReceiptReviewState.Pending,
            receiptKey: "orgs/fake/receipts/fake/fake.jpg");

        var enrollmentId = await EnrollmentIdFor(fx.OrgId, fx.ChargeId);
        var charge = (await ReadAccountAsync(fx.OrgId, enrollmentId)).Single();

        charge.Status.Should().Be(ChargeStatus.Paid);
    }

    [Fact]
    public async Task Review_Verify_MarksItVerified()
    {
        var fx = await SeedChargeAsync();
        var paymentId = await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Transfer, ReceiptReviewState.Pending,
            receiptKey: "orgs/fake/receipts/fake/fake.jpg");

        Assert.IsType<NoContent>(await ReviewAsync(fx.OrgId, paymentId, verified: true, reason: null));
    }

    [Fact]
    public void ReviewValidator_RejectingWithNoReason_IsInvalid()
    {
        var result = new ReviewPayment.Validator().Validate(new ReviewPayment.Request(false, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReviewValidator_VerifyingWithNoReason_IsValid()
    {
        var result = new ReviewPayment.Validator().Validate(new ReviewPayment.Request(true, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Review_APaymentAlreadyReviewed_ReturnsConflict()
    {
        var fx = await SeedChargeAsync();

        // Cash is born Verified, so this never was Pending to begin with.
        var paymentId = Assert.IsType<Created<RecordPayment.Response>>(
            await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null)).Value!.Id;

        var result = await ReviewAsync(fx.OrgId, paymentId, verified: true, reason: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Review_AnUnknownPayment_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await ReviewAsync(orgId, Guid.NewGuid(), verified: true, reason: null);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Void_APaymentAlreadyVoided_ReturnsConflict()
    {
        var fx = await SeedChargeAsync();
        var paymentId = Assert.IsType<Created<RecordPayment.Response>>(
            await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null)).Value!.Id;

        await VoidAsync(fx.OrgId, paymentId, "Primera anulación");
        var second = await VoidAsync(fx.OrgId, paymentId, "Segundo intento");

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemHttpResult>(second).StatusCode);
    }

    [Fact]
    public async Task Void_AnUnknownPayment_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        var result = await VoidAsync(orgId, Guid.NewGuid(), "Motivo");

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void VoidValidator_NoReason_IsInvalid()
    {
        var result = new VoidPayment.Validator().Validate(new VoidPayment.Request(""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReadPayment_ACashPayment_HasNoReceiptLink()
    {
        var fx = await SeedChargeAsync();
        var paymentId = Assert.IsType<Created<RecordPayment.Response>>(
            await RecordAsync(fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Cash, receiptDataUrl: null)).Value!.Id;

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPayment.HandleAsync(
            paymentId, context, UnreachableReceipts(OrganizationFor(fx.OrgId)), CancellationToken.None);

        var response = Assert.IsType<Ok<ReadPayment.Response>>(result).Value!;
        response.Method.Should().Be(PaymentMethod.Cash);
        response.ReceiptUrl.Should().BeNull();
    }

    [Fact]
    public async Task ReadPayment_UnknownPayment_ReturnsNotFound()
    {
        var orgId = await SeedOrganizationAsync();

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, orgId);

        var result = await ReadPayment.HandleAsync(
            Guid.NewGuid(), context, UnreachableReceipts(OrganizationFor(orgId)), CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ReadPayment_APaymentWithAReceipt_SignsALink()
    {
        var fx = await SeedChargeAsync();
        var organization = OrganizationFor(fx.OrgId);
        var receiptKey = StorageKeys.PaymentReceipt(fx.OrgId, fx.ChargeId, "jpg");

        var paymentId = await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 50m, PaymentMethod.Transfer, ReceiptReviewState.Pending, receiptKey);

        await using var context = fixture.CreateAppContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);

        var result = await ReadPayment.HandleAsync(
            paymentId, context, SigningReceipts(organization), CancellationToken.None);

        var response = Assert.IsType<Ok<ReadPayment.Response>>(result).Value!;
        response.ReceiptUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PendingReceipts_ListsAPendingTransferButNotAVoidedOrRejectedOne()
    {
        var fx = await SeedChargeAsync();
        var organization = OrganizationFor(fx.OrgId);
        var key = StorageKeys.PaymentReceipt(fx.OrgId, fx.ChargeId, "jpg");

        var pendingId = await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 10m, PaymentMethod.Transfer, ReceiptReviewState.Pending, key);
        await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 20m, PaymentMethod.Transfer, ReceiptReviewState.Rejected, key);
        var voidedPendingId = await SeedPaymentAsync(
            fx.OrgId, fx.ChargeId, 30m, PaymentMethod.Transfer, ReceiptReviewState.Pending, key);

        await using (var context = fixture.CreateAppContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(context, fx.OrgId);
            var toVoid = await context.TuitionPayments.SingleAsync(p => p.Id == voidedPendingId);
            toVoid.VoidedAt = Clock.GetUtcNow();
            toVoid.VoidReason = "Anulado de prueba";
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var readContext = fixture.CreateAppContext();
        await using var readTransaction = await readContext.Database.BeginTransactionAsync();
        await SportFrogDatabaseFixture.SetCurrentOrganizationAsync(readContext, fx.OrgId);

        var result = await ReadPendingReceipts.ListAsync(
            readContext, SigningReceipts(organization), CancellationToken.None);

        var response = Assert.IsType<Ok<List<ReadPendingReceipts.PendingReceipt>>>(result).Value!;
        response.Should().ContainSingle(row => row.Id == pendingId);
    }
}
