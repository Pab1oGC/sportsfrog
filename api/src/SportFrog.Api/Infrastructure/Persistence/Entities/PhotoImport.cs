using System.Text.Json.Serialization;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>Where a batch of photographs has got to.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<PhotoImportState>))]
public enum PhotoImportState
{
    /// <summary>Accepted and waiting for a worker.</summary>
    Queued,

    /// <summary>A worker has it.</summary>
    Running,

    /// <summary>It ran to the end. Individual files may still have failed.</summary>
    Finished,

    /// <summary>It could not run at all, and <c>Failure</c> says why.</summary>
    Failed,
}

/// <summary>What became of one file in the archive.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<PhotoOutcome>))]
public enum PhotoOutcome
{
    /// <summary>Attached to the person whose document names it.</summary>
    Attached,

    /// <summary>Nobody on the register has a document with that name.</summary>
    Unmatched,

    /// <summary>It matched somebody, but the bytes are not an image.</summary>
    Unreadable,

    /// <summary>Two files name the same person, and this is the later one.</summary>
    Duplicate,

    /// <summary>Not a file this accepts — the wrong extension, or empty.</summary>
    Ignored,
}

/// <summary>One file of an uploaded archive, and what happened to it.</summary>
public sealed record PhotoResult(string File, PhotoOutcome Outcome, string? Athlete);

/// <summary>
/// An archive of photographs somebody uploaded, and what became of it.
/// </summary>
/// <remarks>
/// This row exists because the work does not fit in a request. Four hundred
/// photographs are decoded, turned upright, resized and uploaded one by one;
/// nobody holds a browser open for that, so the upload is accepted and the
/// work happens afterwards. Once it does, the only way anyone can find out
/// how it went is a row like this one.
///
/// It is deliberately not tied to a squad import. The spreadsheet arrives
/// from a club secretary and the photographs arrive later, incomplete, from
/// somebody else — that is how the two actually turn up, and making them one
/// upload would mean waiting for both.
/// </remarks>
public sealed class PhotoImport
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>What the uploader called it, kept so a listing is readable.</summary>
    public required string FileName { get; set; }

    /// <summary>The archive itself, in object storage.</summary>
    public required string ArchiveKey { get; set; }

    public PhotoImportState Status { get; set; } = PhotoImportState.Queued;

    public int Total { get; set; }

    public int Matched { get; set; }

    public int Failed { get; set; }

    /// <summary>
    /// One entry per file. Named rather than merely counted: "seven failed"
    /// is not something an operator can act on, and "IMG_2831.jpg matched
    /// nobody" is.
    /// </summary>
    public IReadOnlyList<PhotoResult> Results { get; set; } = [];

    /// <summary>
    /// Why the batch as a whole could not run. A file that failed is not this
    /// — those are in <see cref="Results"/>, and the batch still finished.
    /// </summary>
    public string? Failure { get; set; }

    public Guid RequestedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
