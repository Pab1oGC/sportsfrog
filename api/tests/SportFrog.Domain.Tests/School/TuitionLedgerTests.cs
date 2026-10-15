using AwesomeAssertions;
using SportFrog.Domain.School;

namespace SportFrog.Domain.Tests.School;

/// <summary>
/// The single place a pension charge's status is decided — see <see
/// cref="TuitionLedger"/>'s own remarks for why no column stores it
/// instead. Every test here checks that the status matches the numbers,
/// never that some other code remembered to update it.
/// </summary>
public sealed class TuitionLedgerTests
{
    private static LedgerEntry Resolve(
        decimal amount,
        IReadOnlyList<decimal> payments,
        bool waived = false,
        bool voided = false) =>
        TuitionLedger.Resolve(amount, waived, voided, payments);

    [Fact]
    public void Resolve_NoPayments_IsPendingWithTheFullAmountOwed()
    {
        var entry = Resolve(amount: 300m, payments: []);

        entry.Paid.Should().Be(0m);
        entry.Balance.Should().Be(300m);
        entry.Status.Should().Be(ChargeStatus.Pending);
    }

    [Fact]
    public void Resolve_OnePaymentLessThanTheAmount_IsPartialWithTheRemainderOwed()
    {
        var entry = Resolve(amount: 300m, payments: [120m]);

        entry.Paid.Should().Be(120m);
        entry.Balance.Should().Be(180m);
        entry.Status.Should().Be(ChargeStatus.Partial);
    }

    [Fact]
    public void Resolve_TwoPartialPaymentsThatAddUpToTheAmount_IsPaidWithNoBalance()
    {
        // The whole point of a ledger instead of a column: nobody marks this
        // paid, the second payment arriving is what makes it so.
        var entry = Resolve(amount: 300m, payments: [120m, 180m]);

        entry.Paid.Should().Be(300m);
        entry.Balance.Should().Be(0m);
        entry.Status.Should().Be(ChargeStatus.Paid);
    }

    [Fact]
    public void Resolve_OnePaymentExceedingTheAmount_IsPaidWithANegativeBalanceNotClampedToZero()
    {
        var entry = Resolve(amount: 300m, payments: [350m]);

        entry.Status.Should().Be(ChargeStatus.Paid);
        entry.Balance.Should().Be(-50m); // a credit on record, not hidden behind a floor
    }

    [Fact]
    public void Resolve_ZeroAmountChargeWithNoPayments_IsPaidByDefinition()
    {
        // A scholarship's charge of zero is not a special case anywhere in
        // this function: it is simply already covered.
        var entry = Resolve(amount: 0m, payments: []);

        entry.Status.Should().Be(ChargeStatus.Paid);
        entry.Balance.Should().Be(0m);
    }

    [Fact]
    public void Resolve_Waived_IsWaivedEvenWithNoPayments()
    {
        var entry = Resolve(amount: 300m, payments: [], waived: true);

        entry.Status.Should().Be(ChargeStatus.Waived);
        entry.Balance.Should().Be(300m); // the figure is still shown, just not owed
    }

    [Fact]
    public void Resolve_WaivedAfterAPartialPayment_StillReadsAsWaivedNotPartial()
    {
        var entry = Resolve(amount: 300m, payments: [100m], waived: true);

        entry.Status.Should().Be(ChargeStatus.Waived);
    }

    [Fact]
    public void Resolve_Voided_IsVoidedEvenIfFullyPaidBeforehand()
    {
        // Voiding outranks everything: a charge struck from the books
        // reports as struck, whatever history led up to that.
        var entry = Resolve(amount: 300m, payments: [300m], voided: true);

        entry.Status.Should().Be(ChargeStatus.Voided);
    }

    [Fact]
    public void Resolve_VoidedAndWaived_VoidedWins()
    {
        var entry = Resolve(amount: 300m, payments: [], waived: true, voided: true);

        entry.Status.Should().Be(ChargeStatus.Voided);
    }
}
