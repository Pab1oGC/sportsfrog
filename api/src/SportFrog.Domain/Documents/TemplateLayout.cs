using System.Text.Json.Serialization;

namespace SportFrog.Domain.Documents;

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
