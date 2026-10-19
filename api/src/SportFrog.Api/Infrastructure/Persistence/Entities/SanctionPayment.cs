namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One payment against a <see cref="Sanction"/>'s fine.
/// </summary>
/// <remarks>
/// A sanction can have several of these — two partial payments that
/// together cover the fine — which is why this is its own table rather than
/// columns on the sanction: <c>SportFrog.Domain.Discipline.SanctionLedger.Resolve</c>
/// already takes a figure for what was paid, for exactly this reason, and
/// nothing here writes a running total anywhere.
///
/// Reuses <see cref="PaymentMethod"/> and <see cref="ReceiptReviewState"/>
/// verbatim from <see cref="TuitionPayment"/> rather than forking a second
/// pair of identical enums — a cash payment is a cash payment whether it is
/// clearing a pension or a fine, and a receipt is checked the same way
/// either time.
///
/// Carries no status of its own either, mirroring <see cref="TuitionPayment"/>:
/// whether this payment still counts toward the fine's balance is decided
/// fresh, every time, from <see cref="ReviewState"/> and
/// <see cref="VoidedAt"/> together — rejected or voided, it does not count;
/// pending or verified, it does.
/// </remarks>
public sealed class SanctionPayment
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid SanctionId { get; set; }

    /// <summary>In bolivianos — see this entity's migration for why there is no currency column.</summary>
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>
    /// A key into object storage, or null for a cash payment. Paired with
    /// <see cref="Method"/> by <c>ck_sanction_payment_receipt</c>, the same
    /// shape <see cref="TuitionPayment.ReceiptKey"/> is paired by.
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

    public Sanction? Sanction { get; set; }
}
