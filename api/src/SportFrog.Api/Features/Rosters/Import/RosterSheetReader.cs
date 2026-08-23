using System.Globalization;
using ClosedXML.Excel;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// One line of the spreadsheet, as it was typed.
/// </summary>
/// <remarks>
/// The parsed value and the text it came from are both kept, because a report
/// that says a date is wrong without saying what was written there sends the
/// reader back to the file to find out which of four hundred rows is meant.
/// </remarks>
internal sealed record SheetRow(
    int Number,
    string? Document,
    string? LastName,
    string? FirstName,
    DateOnly? BirthDate,
    string? BirthDateText,
    string? Sex,
    short? Jersey,
    string? JerseyText,
    string? Position,
    string? Guardian,
    string? GuardianPhone);

/// <summary>
/// What a workbook turned out to contain.
/// </summary>
/// <param name="Problem">
/// Something wrong with the file as a whole, which stops it being read at all.
/// Null when it was read.
/// </param>
/// <param name="StampedTeam">The team the template was generated for, if it says.</param>
internal sealed record SheetContents(
    string? Problem,
    Guid? StampedTeam,
    string? StampedSubject,
    IReadOnlyList<SheetRow> Rows);

/// <summary>
/// Turns an uploaded workbook into rows, or explains why it cannot.
/// </summary>
/// <remarks>
/// Everything here is about a file that has been through other people's
/// hands. It was opened in Excel, or in Google Sheets, or emailed and edited
/// on a phone; columns were reordered to read them, an accent was lost, a
/// date was typed the way the person types dates. None of that is a mistake
/// worth refusing a squad over, so the reader is generous about form and
/// strict about meaning.
///
/// It reads and reports. It decides nothing about eligibility — that is the
/// category's business, and it is asked separately, by the code that already
/// answers it for a single registration.
/// </remarks>
internal static class RosterSheetReader
{
    /// <summary>
    /// How a date may be written. The first is what the template hands out;
    /// the rest are what people type when they retype it.
    /// </summary>
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
    ];

    public static SheetContents Read(Stream file)
    {
        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // Deliberately broad. Everything the spreadsheet library can throw
            // at a file that is not a spreadsheet means the same thing to the
            // person who uploaded it, and none of its internal exception types
            // is worth reproducing in a message.
            return new SheetContents(
                "No se pudo abrir el archivo como hoja de cálculo. Tiene que ser el .xlsx que "
                    + "genera el sistema, no un PDF, un CSV ni una foto de uno.",
                null, null, []);
        }

        using (workbook)
        {
            var (team, subject) = ReadStamp(workbook);

            if (FindSheet(workbook) is not { } sheet)
            {
                return new SheetContents(
                    $"El libro no tiene una hoja llamada '{RosterSheet.DataSheet}' y tiene más "
                        + "de una hoja, así que no hay forma de saber cuál es el plantel.",
                    team, subject, []);
            }

            if (MapHeaders(sheet) is not { } headers)
            {
                return new SheetContents(MissingHeaders(sheet), team, subject, []);
            }

            var rows = new List<SheetRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastRow - RosterSheet.HeaderRow > RosterSheet.MaximumRows)
            {
                return new SheetContents(
                    $"La hoja tiene más de {RosterSheet.MaximumRows} filas. Una planilla no es "
                        + "tan larga: hay que dividirla, o revisar que no se haya pegado algo "
                        + "debajo.",
                    team, subject, []);
            }

            for (var number = RosterSheet.FirstDataRow; number <= lastRow; number++)
            {
                if (ReadRow(sheet, headers, number) is { } row)
                {
                    rows.Add(row);
                }
            }

            return new SheetContents(null, team, subject, rows);
        }
    }

    /// <summary>
    /// The sheet the squad is on.
    /// </summary>
    /// <remarks>
    /// By name where the name survived, and otherwise the only sheet there
    /// is. A workbook with one sheet called something else is somebody who
    /// rebuilt the file by hand, and refusing that would be pedantry; a
    /// workbook with several is genuinely ambiguous and worth asking about.
    /// </remarks>
    private static IXLWorksheet? FindSheet(XLWorkbook workbook)
    {
        var named = workbook.Worksheets.FirstOrDefault(candidate =>
            RosterSheet.Normalize(candidate.Name) == RosterSheet.Normalize(RosterSheet.DataSheet));

        if (named is not null)
        {
            return named;
        }

        var visible = workbook.Worksheets
            .Where(candidate => candidate.Visibility == XLWorksheetVisibility.Visible)
            .ToList();

        return visible.Count == 1 ? visible[0] : null;
    }

    /// <summary>
    /// Which column each heading is in, or null if a required one is absent.
    /// </summary>
    private static Dictionary<SheetColumn, int>? MapHeaders(IXLWorksheet sheet)
    {
        var found = new Dictionary<SheetColumn, int>();
        var lastColumn = sheet.Row(RosterSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            var heading = RosterSheet.Normalize(
                sheet.Cell(RosterSheet.HeaderRow, position).GetString());

            var column = RosterSheet.Columns.FirstOrDefault(candidate =>
                RosterSheet.Normalize(candidate.Header) == heading);

            // First occurrence wins. A duplicated heading is somebody's
            // leftover copy of a column, and the one they filled in is the one
            // they put first.
            if (column is not null && !found.ContainsKey(column))
            {
                found[column] = position;
            }
        }

        return RosterSheet.Columns.Where(column => column.Required).All(found.ContainsKey)
            ? found
            : null;
    }

    private static string MissingHeaders(IXLWorksheet sheet)
    {
        var present = new HashSet<string>(StringComparer.Ordinal);
        var lastColumn = sheet.Row(RosterSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            present.Add(RosterSheet.Normalize(sheet.Cell(RosterSheet.HeaderRow, position).GetString()));
        }

        var missing = RosterSheet.Columns
            .Where(column => column.Required && !present.Contains(RosterSheet.Normalize(column.Header)))
            .Select(column => column.Header);

        return "La primera fila de la hoja tiene que nombrar las columnas, y faltan estas: "
            + string.Join(", ", missing)
            + ". Conviene descargar la plantilla de nuevo en vez de rehacerla a mano.";
    }

    /// <summary>Reads one row, or nothing if it is blank.</summary>
    private static SheetRow? ReadRow(
        IXLWorksheet sheet,
        Dictionary<SheetColumn, int> headers,
        int number)
    {
        string? Text(SheetColumn column) =>
            headers.TryGetValue(column, out var position)
                && sheet.Cell(number, position).GetString().Trim() is { Length: > 0 } value
                    ? value
                    : null;

        var document = Text(RosterSheet.Document);
        var lastName = Text(RosterSheet.LastName);
        var firstName = Text(RosterSheet.FirstName);
        var birthText = Text(RosterSheet.BirthDate);
        var sex = Text(RosterSheet.Sex);
        var jerseyText = Text(RosterSheet.Jersey);
        var position = Text(RosterSheet.Position);
        var guardian = Text(RosterSheet.Guardian);
        var guardianPhone = Text(RosterSheet.GuardianPhone);

        // A row where nothing was typed is not an empty row of a squad, it is
        // the end of the squad. Reporting four hundred blank rows as errors is
        // the fastest way to make a report unreadable.
        if (document is null && lastName is null && firstName is null && birthText is null
            && sex is null && jerseyText is null && position is null
            && guardian is null && guardianPhone is null)
        {
            return null;
        }

        return new SheetRow(
            number,
            document,
            lastName,
            firstName,
            ReadDate(sheet, headers, number),
            birthText,
            sex,
            ReadJersey(jerseyText),
            jerseyText,
            position,
            guardian,
            guardianPhone);
    }

    /// <summary>
    /// A date, however it was written.
    /// </summary>
    /// <remarks>
    /// A real date cell is read as a date. Everything else is text, and the
    /// text is tried against the formats people here actually write — the
    /// template hands out year-first, and half the world types day-first, and
    /// both arrive.
    ///
    /// Ambiguity is not guessed at. 03/04/2011 is read as the fourth of March
    /// only if it came in as text, and day-first is tried before month-first
    /// because that is what the keyboard in this part of the world produces.
    /// A date cell never reaches that code at all, which is the reason the
    /// template formats the column as a date.
    /// </remarks>
    private static DateOnly? ReadDate(
        IXLWorksheet sheet,
        Dictionary<SheetColumn, int> headers,
        int number)
    {
        if (!headers.TryGetValue(RosterSheet.BirthDate, out var position))
        {
            return null;
        }

        var cell = sheet.Cell(number, position);

        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var stored))
        {
            return DateOnly.FromDateTime(stored);
        }

        var text = cell.GetString().Trim();

        if (text.Length == 0)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static short? ReadJersey(string? text) =>
        short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    /// <summary>
    /// Reads the hidden sheet that says which team the template was made for.
    /// </summary>
    /// <remarks>
    /// Absent is not an error. Somebody who built the file themselves is
    /// doing something legitimate, and the only thing lost is this particular
    /// safeguard. Present and naming another team is a different matter, and
    /// it is the caller who acts on that.
    /// </remarks>
    private static (Guid? Team, string? Subject) ReadStamp(XLWorkbook workbook)
    {
        var marker = workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, RosterSheet.MarkerSheet, StringComparison.OrdinalIgnoreCase));

        if (marker is null)
        {
            return (null, null);
        }

        var team = Guid.TryParse(marker.Cell(RosterSheet.MarkerTeam, 1).GetString(), out var parsed)
            ? parsed
            : (Guid?)null;

        var subject = marker.Cell(RosterSheet.MarkerSubject, 1).GetString().Trim();

        return (team, subject.Length > 0 ? subject : null);
    }
}
