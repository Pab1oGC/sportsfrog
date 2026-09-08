using System.Globalization;
using ClosedXML.Excel;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Turns an uploaded delegation squad sheet into rows, or explains why it
/// cannot.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterSheetReader"/>, reading
/// <see cref="DelegationRosterSheet"/>'s different columns into a different
/// row shape. Kept as a separate reader rather than generalized into one
/// that serves both: the two sheets differ in column count and in what a row
/// even identifies (a team's category is fixed for the whole file there; a
/// row picks its own here), and folding that difference into one abstraction
/// would make both harder to change for the sake of a resemblance that is
/// only skin deep. The small amount of cell-and-date parsing the two do
/// share is short and stable enough that duplicating it here is cheaper than
/// the risk of refactoring the team-scoped reader, which has no test suite
/// of its own to catch a mistake.
/// </remarks>
internal static class DelegationRosterSheetReader
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
    ];

    public static DelegationSheetContents Read(Stream file)
    {
        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            return new DelegationSheetContents(
                "No se pudo abrir el archivo como hoja de cálculo. Tiene que ser el .xlsx que "
                    + "genera el sistema, no un PDF, un CSV ni una foto de uno.",
                null, null, null, []);
        }

        using (workbook)
        {
            var (competition, club, subject) = ReadStamp(workbook);

            if (FindSheet(workbook) is not { } sheet)
            {
                return new DelegationSheetContents(
                    $"El libro no tiene una hoja llamada '{DelegationRosterSheet.DataSheet}' y "
                        + "tiene más de una hoja, así que no hay forma de saber cuál es la nómina.",
                    competition, club, subject, []);
            }

            if (MapHeaders(sheet) is not { } headers)
            {
                return new DelegationSheetContents(MissingHeaders(sheet), competition, club, subject, []);
            }

            var rows = new List<DelegationSheetRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastRow - DelegationRosterSheet.HeaderRow > DelegationRosterSheet.MaximumRows)
            {
                return new DelegationSheetContents(
                    $"La hoja tiene más de {DelegationRosterSheet.MaximumRows} filas. Una nómina "
                        + "no es tan larga: hay que dividirla, o revisar que no se haya pegado algo "
                        + "debajo.",
                    competition, club, subject, []);
            }

            for (var number = DelegationRosterSheet.FirstDataRow; number <= lastRow; number++)
            {
                if (ReadRow(sheet, headers, number) is { } row)
                {
                    rows.Add(row);
                }
            }

            return new DelegationSheetContents(null, competition, club, subject, rows);
        }
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook)
    {
        var named = workbook.Worksheets.FirstOrDefault(candidate =>
            RosterSheet.Normalize(candidate.Name) == RosterSheet.Normalize(DelegationRosterSheet.DataSheet));

        if (named is not null)
        {
            return named;
        }

        var visible = workbook.Worksheets
            .Where(candidate => candidate.Visibility == XLWorksheetVisibility.Visible)
            .ToList();

        return visible.Count == 1 ? visible[0] : null;
    }

    private static Dictionary<SheetColumn, int>? MapHeaders(IXLWorksheet sheet)
    {
        var found = new Dictionary<SheetColumn, int>();
        var lastColumn = sheet.Row(DelegationRosterSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            var heading = RosterSheet.Normalize(
                sheet.Cell(DelegationRosterSheet.HeaderRow, position).GetString());

            var column = DelegationRosterSheet.Columns.FirstOrDefault(candidate =>
                RosterSheet.Normalize(candidate.Header) == heading);

            if (column is not null && !found.ContainsKey(column))
            {
                found[column] = position;
            }
        }

        return DelegationRosterSheet.Columns.Where(column => column.Required).All(found.ContainsKey)
            ? found
            : null;
    }

    private static string MissingHeaders(IXLWorksheet sheet)
    {
        var present = new HashSet<string>(StringComparer.Ordinal);
        var lastColumn = sheet.Row(DelegationRosterSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            present.Add(RosterSheet.Normalize(sheet.Cell(DelegationRosterSheet.HeaderRow, position).GetString()));
        }

        var missing = DelegationRosterSheet.Columns
            .Where(column => column.Required && !present.Contains(RosterSheet.Normalize(column.Header)))
            .Select(column => column.Header);

        return "La primera fila de la hoja tiene que nombrar las columnas, y faltan estas: "
            + string.Join(", ", missing)
            + ". Conviene descargar la plantilla de nuevo en vez de rehacerla a mano.";
    }

    /// <summary>Reads one row, or nothing if it is blank.</summary>
    private static DelegationSheetRow? ReadRow(
        IXLWorksheet sheet,
        Dictionary<SheetColumn, int> headers,
        int number)
    {
        string? Text(SheetColumn column) =>
            headers.TryGetValue(column, out var position)
                && sheet.Cell(number, position).GetString().Trim() is { Length: > 0 } value
                    ? value
                    : null;

        var document = Text(DelegationRosterSheet.Document);
        var lastName = Text(DelegationRosterSheet.LastName);
        var firstName = Text(DelegationRosterSheet.FirstName);
        var birthText = Text(DelegationRosterSheet.BirthDate);
        var sex = Text(DelegationRosterSheet.Sex);
        var category = Text(DelegationRosterSheet.Category);
        var guardian = Text(DelegationRosterSheet.Guardian);
        var guardianPhone = Text(DelegationRosterSheet.GuardianPhone);

        if (document is null && lastName is null && firstName is null && birthText is null
            && sex is null && category is null && guardian is null && guardianPhone is null)
        {
            return null;
        }

        return new DelegationSheetRow(
            number,
            document,
            lastName,
            firstName,
            ReadDate(sheet, headers, number),
            birthText,
            sex,
            category,
            guardian,
            guardianPhone);
    }

    private static DateOnly? ReadDate(
        IXLWorksheet sheet,
        Dictionary<SheetColumn, int> headers,
        int number)
    {
        if (!headers.TryGetValue(DelegationRosterSheet.BirthDate, out var position))
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

    /// <summary>Reads the hidden sheet that says which competition and club the template was made for.</summary>
    private static (Guid? Competition, Guid? Club, string? Subject) ReadStamp(XLWorkbook workbook)
    {
        var marker = workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, DelegationRosterSheet.MarkerSheet, StringComparison.OrdinalIgnoreCase));

        if (marker is null)
        {
            return (null, null, null);
        }

        var competition = Guid.TryParse(
            marker.Cell(DelegationRosterSheet.MarkerCompetition, 1).GetString(), out var parsedCompetition)
            ? parsedCompetition
            : (Guid?)null;

        var club = Guid.TryParse(
            marker.Cell(DelegationRosterSheet.MarkerClub, 1).GetString(), out var parsedClub)
            ? parsedClub
            : (Guid?)null;

        var subject = marker.Cell(DelegationRosterSheet.MarkerSubject, 1).GetString().Trim();

        return (competition, club, subject.Length > 0 ? subject : null);
    }
}
