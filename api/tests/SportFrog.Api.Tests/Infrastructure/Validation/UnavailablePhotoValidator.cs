using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Tests.Infrastructure.Validation;

/// <summary>
/// A validator that never answers. For tests that do not exercise validation,
/// so that the photograph is stored as not evaluated and nothing is judged.
/// </summary>
internal sealed class UnavailablePhotoValidator : IPhotoValidator
{
    public Task<PhotoVerdict> ValidateAsync(byte[] photo, CancellationToken cancellationToken) =>
        throw new PhotoValidatorUnavailableException("No validator is wired in this test.");
}
