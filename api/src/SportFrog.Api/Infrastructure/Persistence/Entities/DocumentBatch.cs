namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A request to print documents, and what came of it.
/// </summary>
/// <remarks>
/// Holds the request — which design, for which competition, narrowed to which
/// category or team — and the counts. It does not hold the documents: those
/// are rows of their own that point back here, because a printed credential
/// is a fact about a person and not a line in a progress report.
///
/// What it does hold is everybody who was passed over. There is nowhere else
/// for them to be, and they are the half of the report an operator actually
/// has to act on.
/// </remarks>
public sealed class DocumentBatch
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public DocumentKind Kind { get; set; }

    public Guid TemplateId { get; set; }

    public int TemplateVersion { get; set; }

    public Guid CompetitionId { get; set; }

    /// <summary>Narrows the batch to one division. Null means the whole competition.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Narrows it further to one team.</summary>
    public Guid? TeamId { get; set; }

    public string? CertificateType { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public DocumentBatchState Status { get; set; } = DocumentBatchState.Queued;

    public int Total { get; set; }

    public int Issued { get; set; }

    public int Skipped { get; set; }

    /// <summary>The imposed sheet, which is what goes to a printer.</summary>
    public string? SheetKey { get; set; }

    public IReadOnlyList<DocumentProblem> Problems { get; set; } = [];

    /// <summary>Why the batch as a whole could not run.</summary>
    public string? Failure { get; set; }

    public Guid RequestedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public DocumentTemplate? Template { get; set; }
    public Competition? Competition { get; set; }
}
