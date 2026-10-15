using System.Text.Json.Serialization;

namespace SportFrog.Domain.School;

/// <summary>What has become of one month's pension.</summary>
/// <remarks>
/// Never written to a column — see <see cref="TuitionLedger"/>'s own
/// remarks for why. This is the vocabulary a reader is shown, not a value
/// anything stores.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<ChargeStatus>))]
public enum ChargeStatus
{
    /// <summary>Nothing paid toward it yet.</summary>
    Pending,

    /// <summary>Paid in part. Something is still owed.</summary>
    Partial,

    /// <summary>Paid in full, or owed nothing to begin with.</summary>
    Paid,

    /// <summary>Forgiven. Nothing is owed, and no payment accounts for that.</summary>
    Waived,

    /// <summary>Struck from the books. It owes nothing and counts toward nothing.</summary>
    Voided,
}

/// <summary>
/// One month's pension, resolved: what it charges, what has actually
/// reached it, and what that makes of it.
/// </summary>
public sealed record LedgerEntry(decimal Charged, decimal Paid, decimal Balance, ChargeStatus Status);

/// <summary>
/// Decides what one pension charge's status is.
/// </summary>
/// <remarks>
/// The one place this is decided. A charge's own row carries no status
/// column at all — see <c>tuition_charges</c>'s own migration comment — so
/// that nobody can write "pagada" by hand onto a charge a payment was later
/// voided from and leave it there. Status is read off the same numbers every
/// time, which is what makes voiding a payment and re-querying the same
/// charge show "partial" again automatically, with nothing to remember to
/// update.
///
/// Voided and waived are facts set elsewhere — an operator's own
/// recorded decision, with a reason, never a conclusion drawn from amounts —
/// so they arrive here as the plain facts they are rather than being
/// inferred. Both outrank the arithmetic: a voided charge reports as voided
/// even if, before it was struck, somebody had already paid into it, and the
/// same for one that was waived.
/// </remarks>
public static class TuitionLedger
{
    /// <param name="amount">What the charge asks for. A free registration's
    /// charge of zero is handled the same way any other is: paid in full by
    /// definition, with nothing special-cased for it.</param>
    /// <param name="waived">Whether an operator forgave this charge.</param>
    /// <param name="voided">Whether an operator struck this charge.</param>
    /// <param name="payments">
    /// Only the amounts that actually count toward this charge — a rejected
    /// or still-pending receipt's amount does not belong in this list, and
    /// deciding which receipts cleared that bar is a question for whoever
    /// calls this, not for the ledger itself.
    /// </param>
    public static LedgerEntry Resolve(
        decimal amount,
        bool waived,
        bool voided,
        IReadOnlyList<decimal> payments)
    {
        var paid = 0m;

        foreach (var payment in payments)
        {
            paid += payment;
        }

        var status = voided
            ? ChargeStatus.Voided
            : waived
                ? ChargeStatus.Waived
                : paid >= amount
                    ? ChargeStatus.Paid
                    : paid > 0m
                        ? ChargeStatus.Partial
                        : ChargeStatus.Pending;

        // Not clamped at zero: a balance that goes negative is an
        // overpayment on record, which is a fact worth keeping rather than
        // hiding behind a floor that would make it look like an ordinary
        // paid charge.
        return new LedgerEntry(amount, paid, amount - paid, status);
    }
}
