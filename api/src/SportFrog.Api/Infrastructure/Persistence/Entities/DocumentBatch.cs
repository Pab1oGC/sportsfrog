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

    /// <summary>The design this batch prints from. Null for a credential, which has no design to point to.</summary>
    public Guid? TemplateId { get; set; }

    public int? TemplateVersion { get; set; }

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

    /// <summary>
    /// What goes to a printer: a certificate's imposed sheet — several to a
    /// page, cut apart — or a credential's batch PDF, every credential of the
    /// batch as its own page of the same file.
    /// </summary>
    public string? SheetKey { get; set; }

    /// <summary>
    /// The accreditation catalogue and legal notice as they stood when this
    /// credential batch was requested. Null for a certificate, which prints
    /// from <see cref="TemplateVersion"/> instead.
    /// </summary>
    public CredentialSnapshot? CredentialSnapshot { get; set; }

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
