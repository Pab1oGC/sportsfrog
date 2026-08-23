using System.Globalization;
using System.Text;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>One column of the squad sheet.</summary>
/// <param name="Header">What the person filling it in reads.</param>
/// <param name="Hint">Shown when the cell is selected, so the rule is where the typing is.</param>
internal sealed record SheetColumn(string Header, string Hint, bool Required);

/// <summary>
/// The shape of the squad sheet — written once, and read by both ends.
/// </summary>
/// <remarks>
/// The template that is handed out and the reader that takes it back are the
/// same definition. Two lists of columns is a file the system produces and
/// then refuses, and the person who finds out is a club secretary at eleven
/// at night with a deadline.
///
/// The headers are in Spanish while the rest of the API is not, and that is
/// deliberate rather than sloppy: this file does not pass through the
/// front-end on its way to anybody. Every other string this API produces
/// reaches a person through a screen somebody else builds and can label; a
/// spreadsheet is opened by whoever was sent it, exactly as it left here.
/// </remarks>
internal static class RosterSheet
{
    public const string DataSheet = "Jugadores";

    /// <summary>Where the team's identity is stamped, out of the way.</summary>
    public const string MarkerSheet = "sportfrog";

    /// <summary>
    /// Bumped when the columns change, so a workbook filled in against an
    /// older template can be recognised rather than misread.
    /// </summary>
    public const int Version = 1;

    public const int HeaderRow = 1;

    public const int FirstDataRow = 2;

    /// <summary>
    /// Past any squad and short of a spreadsheet somebody pasted a database
    /// into. The preview is answered inside the request, so it has to be
    /// bounded by something.
    /// </summary>
    public const int MaximumRows = 1000;

    public static readonly SheetColumn Document =
        new("Documento", "Documento de identidad. Es lo que identifica a la persona: si ya está registrada, se la reutiliza.", Required: true);

    public static readonly SheetColumn LastName =
        new("Apellidos", "Apellidos, como figuran en el documento.", Required: true);

    public static readonly SheetColumn FirstName =
        new("Nombres", "Nombres, como figuran en el documento.", Required: true);

    public static readonly SheetColumn BirthDate =
        new("Fecha de nacimiento", "Fecha, no texto. La categoría admite un rango de fechas y se compara contra esto.", Required: true);

    public static readonly SheetColumn Sex =
        new("Sexo", "F o M. Obligatorio solo si la categoría admite uno de los dos.", Required: false);

    public static readonly SheetColumn Jersey =
        new("Dorsal", "Número de camiseta. Se puede dejar vacío hasta que se repartan.", Required: false);

    public static readonly SheetColumn Position =
        new("Posición", "Puesto en el que juega. Libre.", Required: false);

    public static readonly SheetColumn Guardian =
        new("Apoderado", "Nombre del padre, madre o tutor. Necesario para menores.", Required: false);

    public static readonly SheetColumn GuardianPhone =
        new("Teléfono del apoderado", "Teléfono de contacto del apoderado.", Required: false);

    public static readonly SheetColumn Photo =
        new("Foto", "Solo si el archivo de la foto no se llama como el documento. Normalmente se deja vacío.", Required: false);

    /// <summary>The columns, in the order the template lays them out.</summary>
    public static readonly IReadOnlyList<SheetColumn> Columns =
    [
        Document, LastName, FirstName, BirthDate, Sex,
        Jersey, Position, Guardian, GuardianPhone, Photo,
    ];

    /// <summary>Rows of the hidden sheet that says what this workbook is.</summary>
    public const int MarkerNote = 1;
    public const int MarkerVersion = 2;
    public const int MarkerOrganization = 3;
    public const int MarkerTeam = 4;
    public const int MarkerSubject = 5;

    /// <summary>Where a column sits in the template, one-based.</summary>
    /// <remarks>
    /// Only the template lays columns out by position. A workbook coming back
    /// is read by heading, because by then somebody has had it open.
    /// </remarks>
    public static int PositionOf(SheetColumn column)
    {
        for (var index = 0; index < Columns.Count; index++)
        {
            if (Columns[index] == column)
            {
                return index + 1;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(column), "That column is not on the sheet.");
    }

    /// <summary>
    /// A header reduced to what has to match.
    /// </summary>
    /// <remarks>
    /// Accents, case and stray spaces are removed before comparing, so
    /// "Posicion" and "posición " are the column they obviously are. A
    /// workbook has been through Excel, Google Sheets and somebody's phone by
    /// the time it comes back, and refusing it over a missing accent would be
    /// technically correct and useless.
    ///
    /// The columns are matched by their heading rather than by where they
    /// sit, so moving one is fine and renaming one is not — which is the
    /// right way round: an operator reorders columns to read them, and a
    /// renamed column means they are looking at a different sheet.
    /// </remarks>
    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                stripped.Append(character);
            }
        }

        return string.Join(' ', stripped
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
