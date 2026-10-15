using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Domain.School;

namespace SportFrog.Api.Features.Tuition;

/// <summary>One enrollment's pension charges, each resolved to a status.</summary>
/// <remarks>
/// <see cref="TuitionLedger"/> is the one place that decides
/// Pending/Partial/Paid/Waived/Voided, computed fresh every time from a
/// charge's own amount against whichever of its payments still count — a
/// rejected receipt never did, and a voided payment stops the moment it is
/// voided, both without anything here having to write a status anywhere.
/// </remarks>
public static class ReadTuitionAccount
{
    public sealed record Payment(
        Guid Id,
        decimal AmountBob,
        PaymentMethod Method,
        ReceiptReviewState ReviewState,
        string? ReviewReason,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        bool HasReceipt,
        DateTimeOffset CreatedAt);

    public sealed record Charge(
        Guid Id,
        DateOnly Period,
        decimal AmountBob,
        DateOnly DueOn,
        decimal PaidBob,
        decimal BalanceBob,
        ChargeStatus Status,
        DateTimeOffset? WaivedAt,
        string? WaiverReason,
        DateTimeOffset? VoidedAt,
        string? VoidReason,
        IReadOnlyList<Payment> Payments);

    public static IEndpointRouteBuilder MapReadTuitionAccount(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/enrollments/{enrollmentId:guid}/tuition/account", ListAsync)
            .RequireRole(MembershipRole.Viewer)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseEnrollment, "enrollmentId"))
            .WithName(nameof(ReadTuitionAccount))
            .WithSummary("Reads one enrollment's pension charges, oldest first.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(
        Guid enrollmentId,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        if (!await database.CourseEnrollments.AnyAsync(
                entry => entry.Id == enrollmentId, cancellationToken))
        {
            return Results.NotFound();
        }

        var charges = await database.TuitionCharges
            .Where(charge => charge.EnrollmentId == enrollmentId)
            .OrderBy(charge => charge.Period)
            .ToListAsync(cancellationToken);

        var chargeIds = charges.Select(charge => charge.Id).ToList();

        var paymentsByCharge = (await database.TuitionPayments
                .Where(payment => chargeIds.Contains(payment.ChargeId))
                .OrderBy(payment => payment.CreatedAt)
                .ToListAsync(cancellationToken))
            .GroupBy(payment => payment.ChargeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var response = charges
            .Select(charge =>
            {
                paymentsByCharge.TryGetValue(charge.Id, out var ownPayments);
                ownPayments ??= [];

                // Only what still counts toward the balance -- see this
                // type's own remarks for what excludes a payment here.
                var counted = ownPayments
                    .Where(payment => payment.VoidedAt is null
                        && payment.ReviewState != ReceiptReviewState.Rejected)
                    .Select(payment => payment.AmountBob)
                    .ToList();

                var ledger = TuitionLedger.Resolve(
                    charge.AmountBob,
                    waived: charge.WaivedAt is not null,
                    voided: charge.VoidedAt is not null,
                    payments: counted);

                var payments = ownPayments
                    .Select(payment => new Payment(
                        payment.Id, payment.AmountBob, payment.Method, payment.ReviewState,
                        payment.ReviewReason, payment.VoidedAt, payment.VoidReason,
                        payment.ReceiptKey is not null, payment.CreatedAt))
                    .ToList();

                return new Charge(
                    charge.Id, charge.Period, ledger.Charged, charge.DueOn,
                    ledger.Paid, ledger.Balance, ledger.Status,
                    charge.WaivedAt, charge.WaiverReason,
                    charge.VoidedAt, charge.VoidReason, payments);
            })
            .ToList();

        return Results.Ok(response);
    }
}
