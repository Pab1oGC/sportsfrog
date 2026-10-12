namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// The validator gave no usable answer. The photograph itself is not at fault.
/// </summary>
/// <remarks>
/// Kept apart from the verdict on purpose: a photograph that cannot be checked
/// is a different state from one that failed the check, and the caller must be
/// able to tell them apart to decide what to store.
/// </remarks>
public sealed class PhotoValidatorUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
