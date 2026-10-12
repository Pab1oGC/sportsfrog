using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// The outcome of offering a photograph for an athlete. <see cref="Key"/> is
/// null when the photograph was rejected and therefore not kept; in that case
/// <see cref="Assessment"/> is not evaluated, because there is nothing stored
/// to attach a verdict to.
/// </summary>
public sealed record StoredAthletePhoto(string? Key, PhotoAssessment Assessment);

/// <summary>
/// What the validator concluded about an athlete's photograph, in the shape the
/// athlete row keeps it.
/// </summary>
/// <remarks>
/// A photograph the validator could not check is recorded as not evaluated and
/// is still kept. A validator that is down must not block a registration or an
/// import; only a verdict moves a photograph to approved or rejected.
/// </remarks>
public sealed record PhotoAssessment(
    PhotoValidationState State,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    string? RulesVersion,
    DateTimeOffset? ValidatedAt)
{
    /// <summary>The state of a photograph nobody has checked, or of an athlete with no photograph.</summary>
    public static PhotoAssessment Unevaluated { get; } =
        new(PhotoValidationState.NotEvaluated, [], [], null, null);

    public static PhotoAssessment FromVerdict(PhotoVerdict verdict, DateTimeOffset validatedAt) =>
        new(
            verdict.State == PhotoVerdictState.Approved
                ? PhotoValidationState.Approved
                : PhotoValidationState.Rejected,
            verdict.Reasons,
            verdict.Warnings,
            verdict.RulesVersion,
            validatedAt);

    /// <summary>Copies the assessment onto the athlete row, values and all, so nothing stale survives.</summary>
    public void ApplyTo(Athlete athlete)
    {
        athlete.PhotoValidationState = State;
        athlete.PhotoValidationReasons = Reasons.ToList();
        athlete.PhotoValidationWarnings = Warnings.ToList();
        athlete.PhotoRulesVersion = RulesVersion;
        athlete.PhotoValidatedAt = ValidatedAt;
    }
}
