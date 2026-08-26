namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A design for a credential or a certificate.
/// </summary>
/// <remarks>
/// Holds the current layout, which is what an editor opens and what the next
/// batch prints. Every layout it has ever held is kept in
/// <see cref="DocumentTemplateVersion"/>, because an issued document names
/// the version it was printed from and reissuing it has to produce the same
/// card rather than this season's design.
///
/// Deleted logically and never physically. Issued documents reference it with
/// ON DELETE RESTRICT, so removing the row would mean removing the record
/// that somebody was ever accredited.
/// </remarks>
public sealed class DocumentTemplate
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public DocumentKind Kind { get; set; }

    public required string Name { get; set; }

    public required TemplateLayout Layout { get; set; }

    /// <summary>The paper or card it is printed on, from the published set.</summary>
    public string PageSize { get; set; } = "credential";

    /// <summary>
    /// The one offered first for this kind. At most one per kind, which the
    /// application keeps rather than an index: making it a constraint would
    /// mean no organization could ever have a moment with none.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>Which stored version the current layout is.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// What a design looked like at one moment.
/// </summary>
/// <remarks>
/// Written when a layout is saved and never touched again — the application
/// user holds no UPDATE or DELETE on the table, so this is enforced rather
/// than intended. An issued document points at one of these rows, and that is
/// the whole reason a credential printed last season comes out of a reprint
/// looking like last season's credential.
/// </remarks>
public sealed class DocumentTemplateVersion
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid TemplateId { get; set; }

    public int Version { get; set; }

    public required TemplateLayout Layout { get; set; }

    public required string PageSize { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DocumentTemplate? Template { get; set; }
}
