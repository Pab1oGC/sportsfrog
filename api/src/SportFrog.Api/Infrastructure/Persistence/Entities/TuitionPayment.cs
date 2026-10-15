using System.Text.Json.Serialization;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>How a payment reached the organization.</summary>
/// <remarks>
/// <see cref="Cash"/> is handed over in person and needs nothing to check it
/// against; the other two are claims backed by a receipt — see
/// <see cref="TuitionPayment.ReceiptKey"/>'s own remarks for the pairing
/// this decides.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<PaymentMethod>))]
public enum PaymentMethod
{
    Cash,
    Transfer,
    Other,
}

/// <summary>Whether a receipt has been checked against what it claims.</summary>
/// <remarks>
/// A cash payment is born <see cref="Verified"/> — the operator entering it
/// is the verification, and <c>AuditEntry</c> already names who. One with a
/// receipt is born <see cref="Pending"/> until somebody checks it.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<ReceiptReviewState>))]
public enum ReceiptReviewState
{
    Pending,
    Verified,
    Rejected,
}

/// <summary>
/// One payment against a <see cref="TuitionCharge"/>.
/// </summary>
/// <remarks>
/// A charge can have several of these — two partial payments that together
/// cover it — which is why this is its own table rather than columns on
/// the charge: <c>SportFrog.Domain.School.TuitionLedger.Resolve</c> already
/// takes a list of payments for exactly that reason, and nothing here
/// writes a running total anywhere.
///
/// Carries no status of its own either. Whether this payment still counts
/// toward its charge's balance is decided fresh, every time, from
/// <see cref="ReviewState"/> and <see cref="VoidedAt"/> together: rejected
/// or voided, it does not count; pending or verified, it does — see
/// <c>ReadTuitionAccount</c> for where that is actually applied.
/// </remarks>
public sealed class TuitionPayment
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid ChargeId { get; set; }

    public decimal AmountBob { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>
    /// A key into object storage, or null for a cash payment. Paired with
    /// <see cref="Method"/> by <c>ck_payment_receipt</c>: cash has nothing
    /// to scan, and every other method is only trusted once there is
    /// something to check it against.
    /// </summary>
    public string? ReceiptKey { get; set; }

    public ReceiptReviewState ReviewState { get; set; }

    /// <summary>Why a receipt was rejected. Set exactly when <see cref="ReviewState"/> is <see cref="ReceiptReviewState.Rejected"/>.</summary>
    public string? ReviewReason { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public TuitionCharge? Charge { get; set; }
}
