using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>Whether a field prints words or a picture.</summary>
internal enum FieldShape
{
    /// <summary>Placed at a point and sized by its height.</summary>
    Text,

    /// <summary>Placed in a box and fitted inside it.</summary>
    Image,
}

/// <summary>Something a document can print.</summary>
internal sealed record FieldSource(
    string Code,
    string Label,
    FieldShape Shape,
    DocumentKind[] Kinds);

/// <summary>A typeface both the editor and the generator can render.</summary>
internal sealed record DesignFont(string Code, string Label, string Family);

/// <summary>A size a document is printed at, in millimetres.</summary>
internal sealed record PageSize(string Code, string Label, double Width, double Height);

/// <summary>
/// What a design may be made of.
/// </summary>
/// <remarks>
/// Published, because the layout editor lives in a browser and the generator
/// lives here, and they have to agree about every one of these lists. A
/// front-end holding its own copy would offer a field the generator cannot
/// print, and the discovery would be four hundred credentials with a blank
/// space where the club's name was meant to be.
///
/// Closed, because the alternative is an expression language in a jsonb
/// column. Everything in here is a name this code knows how to resolve
/// against a real row; anything else is refused when the layout is saved.
/// </remarks>
internal static class TemplateDesign
{
    private static readonly DocumentKind[] Both =
        [DocumentKind.Credential, DocumentKind.Certificate];

    private static readonly DocumentKind[] Credentials = [DocumentKind.Credential];

    private static readonly DocumentKind[] Certificates = [DocumentKind.Certificate];

    /// <summary>
    /// Everything a document can carry.
    /// </summary>
    /// <remarks>
    /// A credential shows the identity document and the date of birth, and
    /// that is right: it is checked by a referee against the person standing
    /// in front of them, and it is handed to that person. It is the one place
    /// in this system where those may be printed — the public view never sees
    /// them (RNF-16), and a certificate has no business carrying them either,
    /// which is why they are offered for one kind and not the other.
    /// </remarks>
    public static readonly IReadOnlyList<FieldSource> Sources =
    [
        new("text", "Texto fijo", FieldShape.Text, Both),

        new("athlete.full_name", "Nombre completo", FieldShape.Text, Both),
        new("athlete.first_name", "Nombres", FieldShape.Text, Both),
        new("athlete.last_name", "Apellidos", FieldShape.Text, Both),
        new("athlete.photo", "Fotografía", FieldShape.Image, Credentials),
        new("athlete.document", "Documento de identidad", FieldShape.Text, Credentials),
        new("athlete.birth_date", "Fecha de nacimiento", FieldShape.Text, Credentials),

        new("roster.jersey_number", "Dorsal", FieldShape.Text, Credentials),
        new("roster.position", "Posición", FieldShape.Text, Credentials),

        new("team.name", "Equipo", FieldShape.Text, Both),
        new("club.name", "Club", FieldShape.Text, Both),
        new("category.name", "Categoría", FieldShape.Text, Both),

        new("competition.name", "Competencia", FieldShape.Text, Both),
        new("competition.season", "Temporada", FieldShape.Text, Both),
        new("organization.name", "Organización", FieldShape.Text, Both),

        new("certificate.type", "Motivo del certificado", FieldShape.Text, Certificates),

        new("document.serial", "Número de serie", FieldShape.Text, Both),
        new("document.qr", "Código QR de verificación", FieldShape.Image, Both),
        new("document.issued_at", "Fecha de emisión", FieldShape.Text, Both),
        new("document.valid_from", "Válida desde", FieldShape.Text, Credentials),
        new("document.valid_to", "Válida hasta", FieldShape.Text, Credentials),
    ];

    /// <summary>
    /// The typefaces on offer.
    /// </summary>
    /// <remarks>
    /// The three families every PDF reader already has, so nothing has to be
    /// embedded and nothing can fail to render on somebody's machine. The
    /// family string is a CSS stack the editor can apply directly, so what is
    /// dragged around on screen is close to what comes out of the printer.
    ///
    /// A league that wants its own typeface needs the font file shipped with
    /// the generator, which is a decision with a licence attached and not one
    /// to make by accident.
    /// </remarks>
    public static readonly IReadOnlyList<DesignFont> Fonts =
    [
        new("sans", "Sans", "Helvetica, Arial, sans-serif"),
        new("serif", "Serif", "Times New Roman, Times, serif"),
        new("mono", "Monoespaciada", "Courier New, Courier, monospace"),
    ];

    /// <summary>
    /// What it is printed on.
    /// </summary>
    /// <remarks>
    /// The credential sizes are CR80, which is the bank-card size every badge
    /// printer and every lanyard holder is built for. The rest are the paper a
    /// certificate is printed on.
    /// </remarks>
    public static readonly IReadOnlyList<PageSize> PageSizes =
    [
        new("credential", "Credencial horizontal (85.6 × 54 mm)", 85.6, 54),
        new("credential_portrait", "Credencial vertical (54 × 85.6 mm)", 54, 85.6),
        new("a4", "A4 vertical (210 × 297 mm)", 210, 297),
        new("a4_landscape", "A4 horizontal (297 × 210 mm)", 297, 210),
        new("a5", "A5 horizontal (210 × 148 mm)", 210, 148),
        new("letter", "Carta vertical (216 × 279 mm)", 216, 279),
    ];

    public static readonly IReadOnlyList<string> Alignments = ["left", "center", "right"];

    /// <summary>What happens when the words do not fit the space.</summary>
    public static readonly IReadOnlyList<string> Fits = ["shrink", "wrap", "truncate"];

    public const string DefaultFont = "sans";
    public const string DefaultAlign = "left";
    public const string DefaultFit = "shrink";

    public static FieldSource? Source(string code) =>
        Sources.FirstOrDefault(source =>
            string.Equals(source.Code, code, StringComparison.Ordinal));

    public static bool HasFont(string code) =>
        Fonts.Any(font => string.Equals(font.Code, code, StringComparison.Ordinal));

    public static bool HasPageSize(string code) =>
        PageSizes.Any(size => string.Equals(size.Code, code, StringComparison.Ordinal));
}
