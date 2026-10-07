using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// What a reader is told about an athlete's photograph: whether the validator
/// accepted it, and why it did not.
/// </summary>
/// <remarks>
/// The state is a wire name, not the enum, so that a client compares strings
/// that are stable. Every endpoint that answers a verdict uses <see cref="ToWire"/>,
/// so the names cannot drift apart between them.
///
/// Shown only to the organization that holds the athlete, through endpoints
/// that already require a membership. It never reaches the public view.
/// </remarks>
public sealed record PhotoValidationView(
    string State,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    string? RulesVersion,
    DateTimeOffset? ValidatedAt)
{
    public static PhotoValidationView From(Athlete athlete) => new(
        ToWire(athlete.PhotoValidationState),
        athlete.PhotoValidationReasons,
        athlete.PhotoValidationWarnings,
        athlete.PhotoRulesVersion,
        athlete.PhotoValidatedAt);

    public static string ToWire(PhotoValidationState state) => state switch
    {
        PhotoValidationState.Approved => "approved",
        PhotoValidationState.Rejected => "rejected",
        PhotoValidationState.NotEvaluated => "not_evaluated",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown photo validation state."),
    };
}
