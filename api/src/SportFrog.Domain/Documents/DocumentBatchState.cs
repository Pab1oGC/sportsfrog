using System.Text.Json.Serialization;

namespace SportFrog.Domain.Documents;

/// <summary>Where a batch of documents has got to.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<DocumentBatchState>))]
public enum DocumentBatchState
{
    Queued,
    Running,

    /// <summary>It ran to the end. Individual subjects may still have been skipped.</summary>
    Finished,

    /// <summary>It could not run at all, and <c>Failure</c> says why.</summary>
    Failed,
}

/// <summary>Why one subject did not get a document.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<SkipReason>))]
public enum SkipReason
{
    /// <summary>The design prints a photograph and this person has none.</summary>
    NoPhoto,

    /// <summary>They already hold one for this competition.</summary>
    AlreadyIssued,

    /// <summary>The card itself could not be composed. <c>Detail</c> says what happened.</summary>
    CouldNotPrint,
}

/// <summary>One subject the batch passed over, and why.</summary>
public sealed record DocumentProblem(string Subject, SkipReason Reason, string? Detail = null);
