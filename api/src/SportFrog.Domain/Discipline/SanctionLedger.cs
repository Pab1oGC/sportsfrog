using System.Text.Json.Serialization;

namespace SportFrog.Domain.Discipline;

/// <summary>
/// How a sanction is satisfied: a number of matches sat out, a fine paid, or
/// both at once.
/// </summary>
/// <remarks>
/// Decided once, when the sanction is imposed, and never read as a lesser
/// version of itself: <see cref="Both"/> is not "matches, and maybe a fine
/// too" — it means neither alone clears it, the same way a category's
/// eligibility rules all have to hold at once rather than any one of them
/// being enough.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<SanctionCompletionRule>))]
public enum SanctionCompletionRule
{
    /// <summary>Cleared by sitting out a number of matches, and nothing else.</summary>
    Matches,

    /// <summary>Cleared by a fine, and nothing else.</summary>
    Fine,

    /// <summary>Cleared only once both the matches and the fine are.</summary>
    Both,
}

/// <summary>What has become of a sanction.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<SanctionStatus>))]
public enum SanctionStatus
{
    /// <summary>Still owed, in part or in full.</summary>
    Active,

    /// <summary>Served in full: every term it carried has been met.</summary>
    Served,

    /// <summary>Overturned on appeal. Never served, because it was never owed.</summary>
    Lifted,

    /// <summary>Struck from the books. It was raised by mistake and never happened.</summary>
    Voided,
}

/// <summary>
/// A sanction, resolved: what it still owes, and what that makes of it.
/// </summary>
public sealed record SanctionResolution(
    SanctionStatus Status,

    /// <summary>
    /// How many more matches it takes to clear the matches term, or null
    /// where there is no such term (<see cref="SanctionCompletionRule.Fine"/>)
    /// or the sanction no longer owes anything at all.
    /// </summary>
    int? MatchesRemaining,

    /// <summary>
    /// What is still owed on the fine, or null where there is no fine term
    /// (<see cref="SanctionCompletionRule.Matches"/>) or the sanction no
    /// longer owes anything at all.
    /// </summary>
    /// <remarks>
    /// Not clamped at zero, the same as <see cref="School.LedgerEntry.Balance"/>
    /// and for the same reason: an amount paid past what was owed is a credit
    /// on record, not a mistake to hide behind a floor.
    /// </remarks>
    decimal? FineBalance)
{
    /// <summary>
    /// Whether this sanction, on its own, is a reason to refuse somebody
    /// entry today.
    /// </summary>
    public bool BlocksEntry => Status == SanctionStatus.Active;
}

/// <summary>
/// Decides what one sanction's status is.
/// </summary>
/// <remarks>
/// The discipline module's counterpart to
/// <see cref="School.TuitionLedger"/>, resolved the same way: fresh, from the
/// sanction's own terms against whatever has actually been served, never
/// written down on the row and read back — see that type's own remarks for
/// why a status column would let somebody write "cumplida" by hand and leave
/// it there after an appeal reopened it.
///
/// Voided outranks everything, the same way a voided pension charge outranks
/// a waiver already on it: a sanction struck as a mistake never happened,
/// whatever was served against it in the meantime. Lifted outranks the terms
/// for a different reason — an appeal that succeeds says the sanction was
/// never owed, not that it is now satisfied by coincidence.
///
/// <paramref name="matchesServed"/> and <paramref name="finePaid"/> arrive
/// already computed, the same way <c>TuitionLedger.Resolve</c> takes a list
/// of payments rather than a charge id to go and read: deciding which matches
/// count as served and which payments count toward the fine is a question for
/// whoever reads the calendar and the receipts, not for this.
/// </remarks>
public static class SanctionLedger
{
    public static SanctionResolution Resolve(
        SanctionCompletionRule rule,
        int? matchesRequired,
        int matchesServed,
        decimal? fineAmount,
        decimal finePaid,
        bool fineWaived,
        bool lifted,
        bool voided)
    {
        if (voided)
        {
            return new SanctionResolution(SanctionStatus.Voided, MatchesRemaining: null, FineBalance: null);
        }

        if (lifted)
        {
            return new SanctionResolution(SanctionStatus.Lifted, MatchesRemaining: null, FineBalance: null);
        }

        var matchesRemaining = rule == SanctionCompletionRule.Fine
            ? (int?)null
            : Math.Max(0, (matchesRequired ?? 0) - matchesServed);

        var fineBalance = rule == SanctionCompletionRule.Matches
            ? (decimal?)null
            : fineWaived ? 0m : (fineAmount ?? 0m) - finePaid;

        var matchesSatisfied = rule == SanctionCompletionRule.Fine || matchesRemaining <= 0;
        var fineSatisfied = rule == SanctionCompletionRule.Matches || fineWaived || fineBalance <= 0m;

        var served = rule switch
        {
            SanctionCompletionRule.Matches => matchesSatisfied,
            SanctionCompletionRule.Fine => fineSatisfied,
            SanctionCompletionRule.Both => matchesSatisfied && fineSatisfied,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown completion rule."),
        };

        return new SanctionResolution(
            served ? SanctionStatus.Served : SanctionStatus.Active,
            matchesRemaining,
            fineBalance);
    }
}
