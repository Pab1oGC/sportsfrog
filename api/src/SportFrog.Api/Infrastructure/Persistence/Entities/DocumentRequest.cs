namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// An organization's ask to have a batch of documents printed.
/// </summary>
/// <remarks>
/// Documents are issued by SportFrog itself, never by the organization the
/// competition belongs to — see <c>PlatformAuthority</c> and
/// <c>RequestDocumentBatch</c>'s own gate. This is what an organization
/// raises instead: the same facts a document batch needs, without the
/// authority to start one. Approving it is what turns it into a
/// <see cref="DocumentBatch"/>; this row never prints anything by itself.
///
/// Credential and certificate requests share this table rather than two
/// near-identical ones, the same reason <see cref="DocumentBatch"/> and
/// <see cref="IssuedDocument"/> already carry both kinds: the lifecycle —
/// pending, then approved, rejected or cancelled — is the same ask about a
/// different document, not a different ask.
/// </remarks>
public sealed class DocumentRequest
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public DocumentKind Kind { get; set; }

    /// <summary>The design asked for. Null for a credential, which has none to name.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Why a certificate is asked for. Null for a credential.</summary>
    public string? CertificateType { get; set; }

    public Guid CompetitionId { get; set; }

    /// <summary>Narrows the request to one division. Null means the whole competition.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Narrows it further to one team.</summary>
    public Guid? TeamId { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public DocumentRequestState Status { get; set; } = DocumentRequestState.Pending;

    public Guid RequestedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the organization withdrew it. Null until then.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Who at the platform organization approved or rejected it. Null while pending.</summary>
    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Why it was refused. Null unless <see cref="Status"/> is <see cref="DocumentRequestState.Rejected"/>.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>The batch approving it raised. Null until approved.</summary>
    public Guid? BatchId { get; set; }

    public Competition? Competition { get; set; }
}
