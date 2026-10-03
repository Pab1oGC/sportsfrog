using ClosedXML.Excel;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Lists;

/// <summary>Renders a <see cref="ListTable"/> to an Excel workbook.</summary>
/// <remarks>
/// One worksheet per <see cref="ListSection"/> — a metric's own board, a
/// group's own table — so a list that is naturally several boards is several
/// sheets, the same way <see cref="ListPdf"/> turns each into its own
/// labeled block instead of running them together under one heading. Each
/// sheet carries the same mark and accent colour <see cref="ListPdf"/>
/// draws in its own header, for the same reason: the exported file should
/// read as the same competition's document, not a second, uncoloured one.
/// </remarks>
internal static class ListXlsx
{
    public const string MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int TitleRow = 1;
    private const int SubtitleRow = 2;
    private const int HeaderRow = 4;
    private const int FirstDataRow = 5;

    /// <summary>
    /// The same blue <c>ListPdf.Accent</c> — and <c>FixturePdf</c>'s and
    /// <c>CompetitionBulletinPdf</c>'s own — falls back to once a
    /// competition never set an accent colour of its own. Kept as the same
    /// hex rather than a separately-chosen one so a competition with no
    /// branding still looks like the same system in Excel as it does in PDF.
    /// </summary>
    private const string DefaultAccentColor = "#1976D2";

    /// <param name="branding">
    /// The competition's own mark and colour — <see cref="ListBranding"/>
    /// resolves it from whichever category or team the list was scoped to.
    /// Null, the same as <see cref="CompetitionBranding.None"/>, for a list
    /// with no single competition to speak for, or one that never dressed
    /// up its own documents either.
    /// </param>
    public static byte[] Render(ListTable table, CompetitionBranding? branding = null)
    {
        using var workbook = new XLWorkbook();

        if (table.Sections.Count == 0)
        {
            Compose(workbook, table, new ListSection(null, []), branding);
        }
        else
        {
            foreach (var section in table.Sections)
            {
                Compose(workbook, table, section, branding);
            }
        }

        using var file = new MemoryStream();
        workbook.SaveAs(file);
        return file.ToArray();
    }

    private static void Compose(XLWorkbook workbook, ListTable table, ListSection section, CompetitionBranding? branding)
    {
        var sheet = workbook.AddWorksheet(SheetName(workbook, section.Label ?? table.Title));

        // Title and subtitle move one column over to leave the mark its own
        // column — only when there is one to draw; a competition with no
        // logo set keeps the title where it always sat.
        var hasLogo = branding?.LogoBytes is not null;
        var titleColumn = hasLogo ? 2 : 1;

        sheet.Cell(TitleRow, titleColumn).Value = table.Title;
        sheet.Cell(TitleRow, titleColumn).Style.Font.Bold = true;
        sheet.Cell(TitleRow, titleColumn).Style.Font.FontSize = 14;

        if (table.Subtitle is { } subtitle)
        {
            sheet.Cell(SubtitleRow, titleColumn).Value = subtitle;
            sheet.Cell(SubtitleRow, titleColumn).Style.Font.FontColor = AccentColor(branding?.AccentColor);
        }

        if (hasLogo)
        {
            using var logoStream = new MemoryStream(branding!.LogoBytes!);
            // MoveTo before WithSize: ClosedXML's default placement (MoveAndSize)
            // ties the picture's size to its anchor cell and rejects an explicit
            // Width/Height, so the placement has to become Move first.
            sheet.AddPicture(logoStream).MoveTo(sheet.Cell(TitleRow, 1), 2, 2).WithSize(32, 32);
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
    /// The competition's own colour, or the same blue every other document
    /// in this system falls back to once a competition never set one.
    /// </summary>
    // internal, not private: testable directly without rendering a workbook.
    internal static XLColor AccentColor(string? hex) =>
        XLColor.FromHtml(string.IsNullOrWhiteSpace(hex) ? DefaultAccentColor : hex);

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
