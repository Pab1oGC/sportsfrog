namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>Whether a photograph passed the validator's rules.</summary>
public enum PhotoVerdictState
{
    /// <summary>No rule with a calibrated threshold failed.</summary>
    Approved,

    /// <summary>At least one calibrated rule failed.</summary>
    Rejected,
}

/// <summary>
/// The validator's answer for one photograph.
/// </summary>
/// <param name="State">The decision.</param>
/// <param name="Reasons">
/// Why it was rejected: a critical rule failed, in Spanish, ready to show a
/// user. Only these block.
/// </param>
/// <param name="Warnings">
/// Defects that did not block — a non-critical rule failed. The photograph can
/// be kept, and whoever uploads it does so knowing about them.
/// </param>
/// <param name="Unverified">
/// Rules that were not checked at all: no calibrated threshold, or a measure
/// OFIQ could not compute. Not defects, and never a reason to refuse.
/// </param>
/// <param name="RulesVersion">The version of the rules that produced the verdict.</param>
/// <param name="Calibrated">
/// Whether the thresholds have been calibrated. Until they are, a verdict is
/// provisional and should be read as such.
/// </param>
public sealed record PhotoVerdict(
    PhotoVerdictState State,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Unverified,
    string RulesVersion,
    bool Calibrated);
