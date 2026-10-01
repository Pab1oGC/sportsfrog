using ClosedXML.Excel;

namespace SportFrog.Api.Features.Lists;

/// <summary>Renders a <see cref="ListTable"/> to an Excel workbook.</summary>
/// <remarks>
/// One worksheet per <see cref="ListSection"/> — a metric's own board, a
/// group's own table — so a list that is naturally several boards is several
/// sheets, the same way <see cref="ListPdf"/> turns each into its own
/// labeled block instead of running them together under one heading.
/// </remarks>
internal static class ListXlsx
{
    public const string MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int TitleRow = 1;
    private const int SubtitleRow = 2;
    private const int HeaderRow = 4;
    private const int FirstDataRow = 5;

    public static byte[] Render(ListTable table)
    {
        using var workbook = new XLWorkbook();

        if (table.Sections.Count == 0)
        {
            Compose(workbook, table, new ListSection(null, []));
        }
        else
        {
            foreach (var section in table.Sections)
            {
                Compose(workbook, table, section);
            }
        }

        using var file = new MemoryStream();
        workbook.SaveAs(file);
        return file.ToArray();
    }

    private static void Compose(XLWorkbook workbook, ListTable table, ListSection section)
    {
        var sheet = workbook.AddWorksheet(SheetName(workbook, section.Label ?? table.Title));

        sheet.Cell(TitleRow, 1).Value = table.Title;
        sheet.Cell(TitleRow, 1).Style.Font.Bold = true;
        sheet.Cell(TitleRow, 1).Style.Font.FontSize = 14;

        if (table.Subtitle is { } subtitle)
        {
            sheet.Cell(SubtitleRow, 1).Value = subtitle;
            sheet.Cell(SubtitleRow, 1).Style.Font.FontColor = XLColor.FromHtml("#595959");
        }

        if (section.Rows.Count == 0)
        {
            sheet.Cell(HeaderRow, 1).Value = ListMessages.NoData;
            sheet.Cell(HeaderRow, 1).Style.Font.Italic = true;
            return;
        }

        WriteHeader(sheet, table.Columns);
        WriteRows(sheet, table.Columns, section.Rows);

        var lastRow = FirstDataRow + section.Rows.Count - 1;
        var lastColumn = table.Columns.Count;

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Range(HeaderRow, 1, lastRow, lastColumn).SetAutoFilter();
        sheet.Columns(1, lastColumn).AdjustToContents(HeaderRow, HeaderRow, 10d, 40d);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<ListColumn> columns)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            var cell = sheet.Cell(HeaderRow, index + 1);
            cell.Value = columns[index].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }
    }

    private static void WriteRows(IXLWorksheet sheet, IReadOnlyList<ListColumn> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        // Set once per column rather than per cell: the same convention
        // BuildRosterTemplate uses for its own date column, and cheaper than
        // re-applying a style to every one of a column's cells.
        for (var index = 0; index < columns.Count; index++)
        {
            var columnNumber = index + 1;

            switch (columns[index].Kind)
            {
                case ListValueKind.Date:
                    sheet.Column(columnNumber).Style.DateFormat.Format = "dd/mm/yyyy";
                    break;
                case ListValueKind.DateTime:
                    sheet.Column(columnNumber).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                    break;
            }
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var sheetRow = FirstDataRow + rowIndex;

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                WriteCell(sheet.Cell(sheetRow, columnIndex + 1), row[columnIndex], columns[columnIndex].Kind);
            }
        }
    }

    private static void WriteCell(IXLCell cell, object? value, ListValueKind kind)
    {
        if (value is null)
        {
            return;
        }

        switch (kind)
        {
            case ListValueKind.Number:
                cell.Value = ListCellValues.ToNumber(value);
                break;
            case ListValueKind.Boolean:
                cell.Value = ListCellValues.ToBoolean(value);
                break;
            case ListValueKind.Date:
            case ListValueKind.DateTime:
                cell.Value = ListCellValues.ToDateTime(value);
                break;
            default:
                // Text as text, not a number that happens to look like one —
                // the same reason BuildRosterTemplate forces its document-id
                // column to "@": a 0071 that Excel is free to reinterpret is
                // a 0071 that stops being the identifier it was exported as.
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// A sheet name Excel accepts: none of <c>: \ / ? * [ ]</c>, no more than
    /// 31 characters, and not already taken by an earlier section.
    /// </summary>
    private static string SheetName(XLWorkbook workbook, string label)
    {
        var sanitized = Sanitize(label);
        var name = sanitized;
        var suffix = 2;

        while (workbook.Worksheets.Contains(name))
        {
            var trimmed = sanitized.Length > 28 ? sanitized[..28] : sanitized;
            name = $"{trimmed} {suffix}";
            suffix++;
        }

        return name;
    }

    private static readonly char[] SheetNameInvalidCharacters = [':', '\\', '/', '?', '*', '[', ']'];

    private static string Sanitize(string label)
    {
        var cleaned = new string(label
            .Select(character => SheetNameInvalidCharacters.Contains(character) ? ' ' : character)
            .ToArray())
            .Trim();

        if (cleaned.Length == 0)
        {
            cleaned = "Hoja";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
