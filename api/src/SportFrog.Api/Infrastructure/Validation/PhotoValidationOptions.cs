using System.ComponentModel.DataAnnotations;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Where the photo validator listens, and how long to wait for it.
/// </summary>
/// <remarks>
/// The URL is optional on purpose. Without it the API still starts, which
/// keeps local development working when the validator is not running, and
/// every photo is recorded as not evaluated rather than failing the upload.
/// Validation on start is deliberately not used here: unlike the storage
/// credentials, a missing validator is a degraded mode, not a broken deployment.
/// </remarks>
public sealed class PhotoValidationOptions
{
    public const string SectionName = "Validador";

    /// <summary>Base address of the validator, e.g. <c>http://validador:8000</c>.</summary>
    [Url]
    public string? Url { get; set; }

    /// <summary>
    /// Upper bound for one validation. OFIQ takes seconds per photo on CPU,
    /// so the default is generous; a request that exceeds it is treated as the
    /// validator being unavailable.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}
