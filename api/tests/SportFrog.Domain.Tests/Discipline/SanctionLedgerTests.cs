using AwesomeAssertions;
using SportFrog.Domain.Discipline;

namespace SportFrog.Domain.Tests.Discipline;

/// <summary>
/// The single place a sanction's status is decided — see
/// <see cref="SanctionLedger"/>'s own remarks for why no column stores it
/// instead. Every test here checks that the status matches the terms and
/// what was served against them, never that some other code remembered to
/// update it.
/// </summary>
public sealed class SanctionLedgerTests
{
    private static SanctionResolution Resolve(
        SanctionCompletionRule rule,
        int? matchesRequired = null,
        int matchesServed = 0,
        decimal? fineAmount = null,
        decimal finePaid = 0m,
        bool fineWaived = false,
        bool lifted = false,
        bool voided = false) =>
        SanctionLedger.Resolve(
            rule, matchesRequired, matchesServed, fineAmount, finePaid, fineWaived, lifted, voided);

    // ---- Matches only ------------------------------------------------

    [Fact]
    public void Resolve_Matches_NoneServedYet_IsActiveWithEveryMatchRemaining()
    {
        var resolution = Resolve(SanctionCompletionRule.Matches, matchesRequired: 3, matchesServed: 0);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.MatchesRemaining.Should().Be(3);
        resolution.FineBalance.Should().BeNull();
        resolution.BlocksEntry.Should().BeTrue();
    }

    [Fact]
    public void Resolve_Matches_SomeServed_IsActiveWithTheRestRemaining()
    {
        var resolution = Resolve(SanctionCompletionRule.Matches, matchesRequired: 3, matchesServed: 2);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.MatchesRemaining.Should().Be(1);
    }

    [Fact]
    public void Resolve_Matches_ExactlyAsManyServedAsRequired_IsServed()
    {
        var resolution = Resolve(SanctionCompletionRule.Matches, matchesRequired: 3, matchesServed: 3);

        resolution.Status.Should().Be(SanctionStatus.Served);
        resolution.MatchesRemaining.Should().Be(0);
        resolution.BlocksEntry.Should().BeFalse();
    }

    [Fact]
    public void Resolve_Matches_MoreServedThanRequired_IsServedWithRemainingClampedToZero()
    {
        // The team kept playing past the suspension's own count; this is not
        // a deficit to report as negative.
        var resolution = Resolve(SanctionCompletionRule.Matches, matchesRequired: 3, matchesServed: 5);

        resolution.Status.Should().Be(SanctionStatus.Served);
        resolution.MatchesRemaining.Should().Be(0);
    }

    // ---- Fine only -----------------------------------------------------

    [Fact]
    public void Resolve_Fine_NotPaid_IsActiveWithTheFullAmountOwed()
    {
        var resolution = Resolve(SanctionCompletionRule.Fine, fineAmount: 500m, finePaid: 0m);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.FineBalance.Should().Be(500m);
        resolution.MatchesRemaining.Should().BeNull();
    }

    [Fact]
    public void Resolve_Fine_PartiallyPaid_IsActiveWithTheRemainderOwed()
    {
        var resolution = Resolve(SanctionCompletionRule.Fine, fineAmount: 500m, finePaid: 200m);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.FineBalance.Should().Be(300m);
    }

    [Fact]
    public void Resolve_Fine_PaidInFull_IsServed()
    {
        var resolution = Resolve(SanctionCompletionRule.Fine, fineAmount: 500m, finePaid: 500m);

        resolution.Status.Should().Be(SanctionStatus.Served);
        resolution.FineBalance.Should().Be(0m);
    }

    [Fact]
    public void Resolve_Fine_OverPaid_IsServedWithANegativeBalanceNotClampedToZero()
    {
        var resolution = Resolve(SanctionCompletionRule.Fine, fineAmount: 500m, finePaid: 550m);

        resolution.Status.Should().Be(SanctionStatus.Served);
        resolution.FineBalance.Should().Be(-50m); // a credit on record, not hidden behind a floor
    }

    [Fact]
    public void Resolve_Fine_Waived_IsServedWithNoBalanceEvenUnpaid()
    {
        var resolution = Resolve(SanctionCompletionRule.Fine, fineAmount: 500m, finePaid: 0m, fineWaived: true);

        resolution.Status.Should().Be(SanctionStatus.Served);
        resolution.FineBalance.Should().Be(0m);
    }

    // ---- Both ------------------------------------------------------------

    [Fact]
    public void Resolve_Both_OnlyTheMatchesServed_IsStillActive()
    {
        var resolution = Resolve(
            SanctionCompletionRule.Both,
            matchesRequired: 2, matchesServed: 2,
            fineAmount: 500m, finePaid: 0m);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.MatchesRemaining.Should().Be(0);
        resolution.FineBalance.Should().Be(500m);
    }

    [Fact]
    public void Resolve_Both_OnlyTheFinePaid_IsStillActive()
    {
        var resolution = Resolve(
            SanctionCompletionRule.Both,
            matchesRequired: 2, matchesServed: 0,
            fineAmount: 500m, finePaid: 500m);

        resolution.Status.Should().Be(SanctionStatus.Active);
        resolution.MatchesRemaining.Should().Be(2);
        resolution.FineBalance.Should().Be(0m);
    }

    [Fact]
    public void Resolve_Both_MatchesServedAndFinePaid_IsServed()
    {
        var resolution = Resolve(
            SanctionCompletionRule.Both,
            matchesRequired: 2, matchesServed: 2,
            fineAmount: 500m, finePaid: 500m);

        resolution.Status.Should().Be(SanctionStatus.Served);
    }

    [Fact]
    public void Resolve_Both_MatchesServedAndFineWaived_IsServed()
    {
        var resolution = Resolve(
            SanctionCompletionRule.Both,
            matchesRequired: 2, matchesServed: 2,
            fineAmount: 500m, finePaid: 0m, fineWaived: true);

        resolution.Status.Should().Be(SanctionStatus.Served);
    }

    // ---- Lifted and voided outrank the terms ------------------------------

    [Fact]
    public void Resolve_Lifted_IsLiftedEvenWithNothingServed()
    {
        var resolution = Resolve(
            SanctionCompletionRule.Both,
            matchesRequired: 3, matchesServed: 0,
            fineAmount: 500m, finePaid: 0m,
            lifted: true);

        resolution.Status.Should().Be(SanctionStatus.Lifted);
        resolution.MatchesRemaining.Should().BeNull();
        resolution.FineBalance.Should().BeNull();
        resolution.BlocksEntry.Should().BeFalse();
    }

    [Fact]
    public void Resolve_Voided_IsVoidedEvenIfFullyServedBeforehand()
    {
        // Voiding outranks everything: a sanction struck from the books
        // reports as struck, whatever history led up to that.
        var resolution = Resolve(
            SanctionCompletionRule.Matches,
            matchesRequired: 3, matchesServed: 3,
            voided: true);

        resolution.Status.Should().Be(SanctionStatus.Voided);
        resolution.BlocksEntry.Should().BeFalse();
    }

    [Fact]
    public void Resolve_VoidedAndLifted_VoidedWins()
    {
        var resolution = Resolve(SanctionCompletionRule.Matches, matchesRequired: 1, lifted: true, voided: true);

        resolution.Status.Should().Be(SanctionStatus.Voided);
    }
}
