namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Judges whether a photograph is fit for a credential.
/// </summary>
public interface IPhotoValidator
{
    /// <summary>
    /// The verdict for <paramref name="photo"/>, which must be a JPEG: the
    /// normalizer is what produces the bytes this receives.
    /// </summary>
    /// <exception cref="PhotoValidatorUnavailableException">
    /// The validator could not give an answer: it is not configured, it is
    /// unreachable, it timed out, or it answered something that is not a verdict.
    /// </exception>
    Task<PhotoVerdict> ValidateAsync(byte[] photo, CancellationToken cancellationToken);
}
