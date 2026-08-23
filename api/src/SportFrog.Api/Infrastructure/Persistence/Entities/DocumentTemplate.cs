using System.Text.Json.Serialization;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>What a template produces.</summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<DocumentKind>))]
public enum DocumentKind
{
    /// <summary>The card a player carries, checked at the side of the pitch.</summary>
    Credential,

    /// <summary>Given afterwards: took part, came first, refereed.</summary>
    Certificate,
}

/// <summary>
/// One side of a document: a background, and things placed on it.
/// </summary>
/// <param name="BackgroundKey">
/// The artwork, in object storage. Absent means a plain white face, which is
/// what a certificate on headed paper wants.
/// </param>
/// <param name="AspectRatio">
/// Width over height. Kept per face rather than taken from the page size
/// because artwork arrives in whatever shape it arrives in, and the editor
/// has to lay fields out over what the designer actually uploaded.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TemplateFace(
    string? BackgroundKey,
    double AspectRatio,
    IReadOnlyList<TemplateField> Fields);

/// <summary>
/// One thing printed on a face.
/// </summary>
/// <remarks>
/// Every measurement is between 0 and 1, relative to the face. That is the
/// decision the whole design rests on: a layout laid out over a 900-pixel
/// mock-up still prints correctly when somebody replaces the artwork with a
/// 3000-pixel version, and prints correctly again on a card of a different
/// size. Absolute coordinates would tie a design to the resolution of the
/// image that happened to be open when it was made.
/// </remarks>
/// <param name="Source">What to print, from the published set.</param>
/// <param name="Text">The words, when the source is a literal label.</param>
/// <param name="Size">Text height, as a fraction of the face's height.</param>
/// <param name="MinSize">How far <c>shrink</c> may go before it gives up.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TemplateField(
    string Source,
    double X,
    double Y,
    double? W = null,
    double? H = null,
    double? Size = null,
    string? Font = null,
    string? Align = null,
    string? Fit = null,
    double? MinSize = null,
    string? Color = null,
    bool? Bold = null,
    string? Text = null);

/// <summary>
/// The front, and a back if the document has one.
/// </summary>
/// <remarks>
/// These three records refuse properties they do not know, on the way in and
/// on the way out, and that refusal is what the schema means when it says the
/// column does not accept executable content. The guarantee is not a filter
/// hunting for dangerous strings — those are always one trick behind — it is
/// that a layout has no room for anything that was not designed for.
///
/// It carries one rule for whoever changes these records later: a property
/// may be added and must never be removed. The stored versions are an archive
/// that a reissue reads years afterwards, and deleting an obsolete property
/// here would make every credential issued under it unreadable. Mark it
/// obsolete and stop writing it.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TemplateLayout(TemplateFace Front, TemplateFace? Back);

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
