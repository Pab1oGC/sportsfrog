using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes;

/// <summary>
/// Asks the validator about a normalized photograph and turns the answer into
/// the assessment an athlete row keeps.
/// </summary>
/// <remarks>
/// Owns one decision: a validator that gives no answer is recorded as not
/// evaluated, never as a rejection. Storing the photograph is someone else's
/// job, which is what lets this be tested without a bucket.
/// </remarks>
public sealed class PhotoAssessor(
    IPhotoValidator validator,
    TimeProvider time,
    ILogger<PhotoAssessor> logger)
{
    public async Task<PhotoAssessment> AssessAsync(byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            var verdict = await validator.ValidateAsync(content, cancellationToken);
            return PhotoAssessment.FromVerdict(verdict, time.GetUtcNow());
        }
        catch (PhotoValidatorUnavailableException failure)
        {
            logger.LogWarning(failure, "A photograph was stored without an evaluation.");
            return PhotoAssessment.Unevaluated;
        }
    }
}
