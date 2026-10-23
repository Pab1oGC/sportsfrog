using System.Globalization;
using ClosedXML.Excel;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>One line of the staff sheet, as it was typed.</summary>
internal sealed record StaffSheetRow(
    int Number,
    string? Document,
    string? LastName,
    string? FirstName,
    DateOnly? BirthDate,
    string? BirthDateText,
    string? RoleText,
    StaffRole? Role);

/// <summary>What a staff workbook turned out to contain.</summary>
/// <param name="Problem">Something wrong with the file as a whole. Null when it was read.</param>
/// <param name="StampedTeam">The team the template was generated for, if it says.</param>
internal sealed record StaffSheetContents(
    string? Problem,
    Guid? StampedTeam,
    string? StampedSubject,
    IReadOnlyList<StaffSheetRow> Rows);

/// <summary>
/// Turns an uploaded staff workbook into rows, or explains why it cannot.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterSheetReader"/>, reading
/// <see cref="StaffSheet"/>'s own, shorter set of columns. Kept as its own
/// reader rather than folded into that one for the same reason
/// <see cref="DelegationRosterSheetReader"/> is kept separate from it: the
/// two sheets differ in what a row even is, and generalizing over a
/// resemblance that is only skin deep would make both harder to change
/// safely.
/// </remarks>
internal static class StaffSheetReader
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
    ];

    public static StaffSheetContents Read(Stream file)
    {
        if (WorkbookGuard.Inspect(file) is { } problem)
        {
            return new StaffSheetContents(problem, null, null, []);
        }

        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            return new StaffSheetContents(
                "No se pudo abrir el archivo como hoja de cálculo. Tiene que ser el .xlsx que "
                    + "genera el sistema, no un PDF, un CSV ni una foto de uno.",
                null, null, []);
        }

        using (workbook)
        {
            var (team, subject) = ReadStamp(workbook);

            if (FindSheet(workbook) is not { } sheet)
            {
                return new StaffSheetContents(
                    $"El libro no tiene una hoja llamada '{StaffSheet.DataSheet}' y tiene más de "
                        + "una hoja, así que no hay forma de saber cuál es el cuerpo técnico.",
                    team, subject, []);
            }

            if (MapHeaders(sheet) is not { } headers)
            {
                return new StaffSheetContents(MissingHeaders(sheet), team, subject, []);
            }

            var rows = new List<StaffSheetRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            if (lastRow - StaffSheet.HeaderRow > StaffSheet.MaximumRows)
            {
                return new StaffSheetContents(
                    $"La hoja tiene más de {StaffSheet.MaximumRows} filas. Un cuerpo técnico no es "
                        + "tan largo: conviene revisar que no se haya pegado algo debajo.",
                    team, subject, []);
            }

            for (var number = StaffSheet.FirstDataRow; number <= lastRow; number++)
            {
                if (ReadRow(sheet, headers, number) is { } row)
                {
                    rows.Add(row);
                }
            }

            return new StaffSheetContents(null, team, subject, rows);
        }
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook)
    {
        var named = workbook.Worksheets.FirstOrDefault(candidate =>
            RosterSheet.Normalize(candidate.Name) == RosterSheet.Normalize(StaffSheet.DataSheet));

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
        var lastColumn = sheet.Row(StaffSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            var heading = RosterSheet.Normalize(sheet.Cell(StaffSheet.HeaderRow, position).GetString());

            var column = StaffSheet.Columns.FirstOrDefault(candidate =>
                RosterSheet.Normalize(candidate.Header) == heading);

            // First occurrence wins, same reading RosterSheetReader gives a
            // duplicated heading.
            if (column is not null && !found.ContainsKey(column))
            {
                found[column] = position;
            }
        }

        return StaffSheet.Columns.Where(column => column.Required).All(found.ContainsKey)
            ? found
            : null;
    }

    private static string MissingHeaders(IXLWorksheet sheet)
    {
        var present = new HashSet<string>(StringComparer.Ordinal);
        var lastColumn = sheet.Row(StaffSheet.HeaderRow).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var position = 1; position <= lastColumn; position++)
        {
            present.Add(RosterSheet.Normalize(sheet.Cell(StaffSheet.HeaderRow, position).GetString()));
        }

        var missing = StaffSheet.Columns
            .Where(column => column.Required && !present.Contains(RosterSheet.Normalize(column.Header)))
            .Select(column => column.Header);

        return "La primera fila de la hoja tiene que nombrar las columnas, y faltan estas: "
            + string.Join(", ", missing)
            + ". Conviene descargar la plantilla de nuevo en vez de rehacerla a mano.";
    }

    /// <summary>Reads one row, or nothing if it is blank.</summary>
    private static StaffSheetRow? ReadRow(IXLWorksheet sheet, Dictionary<SheetColumn, int> headers, int number)
    {
        string? Text(SheetColumn column) =>
            headers.TryGetValue(column, out var position)
                && sheet.Cell(number, position).GetString().Trim() is { Length: > 0 } value
                    ? value
                    : null;

        var document = Text(StaffSheet.Document);
        var lastName = Text(StaffSheet.LastName);
        var firstName = Text(StaffSheet.FirstName);
        var birthText = Text(StaffSheet.BirthDate);
        var roleText = Text(StaffSheet.Role);

        // A row where nothing was typed is the end of the staff list, not an
        // empty entry in it — same reading RosterSheetReader gives a blank
        // row.
        if (document is null && lastName is null && firstName is null && birthText is null && roleText is null)
        {
            return null;
        }

        return new StaffSheetRow(
            number,
            document,
            lastName,
            firstName,
            ReadDate(sheet, headers, number),
            birthText,
            roleText,
            StaffSheet.RoleOf(roleText));
    }

    private static DateOnly? ReadDate(IXLWorksheet sheet, Dictionary<SheetColumn, int> headers, int number)
    {
        if (!headers.TryGetValue(StaffSheet.BirthDate, out var position))
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

    /// <summary>Reads the hidden sheet that says which team the template was made for.</summary>
    private static (Guid? Team, string? Subject) ReadStamp(XLWorkbook workbook)
    {
        var marker = workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, StaffSheet.MarkerSheet, StringComparison.OrdinalIgnoreCase));

        if (marker is null)
        {
            return (null, null);
        }

        var team = Guid.TryParse(marker.Cell(StaffSheet.MarkerTeam, 1).GetString(), out var parsed)
            ? parsed
            : (Guid?)null;

        var subject = marker.Cell(StaffSheet.MarkerSubject, 1).GetString().Trim();

        return (team, subject.Length > 0 ? subject : null);
    }
}
