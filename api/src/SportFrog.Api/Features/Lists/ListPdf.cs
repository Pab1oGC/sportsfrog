using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Lists;

/// <summary>Renders a <see cref="ListTable"/> to a flowing PDF document.</summary>
/// <remarks>
/// The same flowing-layout and <c>Th</c>/<c>Td</c> shape, and now the same
/// mark-and-accent header, <c>Matches.FixturePdf</c> and the other report
/// renderers already use — a list can be ten rows or ten thousand, and
/// nothing here can know which before it starts laying them out.
/// </remarks>
internal static class ListPdf
{
    public const string MimeType = "application/pdf";

    /// <summary>
    /// Past this many columns a portrait page starts crowding cells into
    /// unreadable widths — a standings table's dozen columns is the case
    /// this exists for.
    /// </summary>
    private const int ColumnsThatForceLandscape = 8;

    /// <param name="branding">
    /// The competition's own mark and colour — <see cref="ListBranding"/>
    /// resolves it from whichever category or team the list was scoped to.
    /// Null, the same as <see cref="CompetitionBranding.None"/>, for a list
    /// with no single competition to speak for (the organization-wide ones)
    /// or a competition that never dressed up its own documents either.
    /// </param>
    public static byte[] Render(ListTable table, CompetitionBranding? branding = null) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(table.Columns.Count > ColumnsThatForceLandscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().Element(container => Header(container, table, branding));
            page.Content().Element(container => Body(container, table));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .GeneratePdf();

    private static void Header(IContainer container, ListTable table, CompetitionBranding? branding) =>
        container.Row(row =>
        {
            if (branding?.LogoBytes is { } logo)
            {
                row.ConstantItem(36).Height(36).Image(logo).FitArea();
                row.ConstantItem(10);
            }

            row.RelativeItem().Column(column =>
            {
                column.Item().Text(table.Title).FontSize(18).Bold();

                if (table.Subtitle is { } subtitle)
                {
                    column.Item().Text(subtitle).FontSize(12).FontColor(Accent(branding?.AccentColor));
                }

                column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
        });

    /// <summary>
    /// The competition's own colour, or the same blue every other document
    /// in this system falls back to once a competition never set one —
    /// <c>FixturePdf.Accent</c> and <c>CompetitionBulletinPdf.Accent</c> are
    /// the same one-liner for the same reason.
    /// </summary>
    // internal, not private: testable directly without rendering a PDF, the
    // same convention ColumnWeight and AlignmentFor above already use.
    internal static Color Accent(string? hex) => string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;

    private static void Body(IContainer container, ListTable table)
    {
        if (table.Sections.Count == 0)
        {
            container.PaddingTop(10).Text(ListMessages.NoData).Italic();
            return;
        }

        container.PaddingTop(10).Column(column =>
        {
            foreach (var section in table.Sections)
            {
                column.Item().Element(sectionContainer => Section(sectionContainer, table.Columns, section));
            }
        });
    }

    private static void Section(IContainer container, IReadOnlyList<ListColumn> columns, ListSection section) =>
        container.PaddingBottom(14).Column(column =>
        {
            if (section.Label is { } label)
            {
                column.Item().PaddingBottom(4).Text(label).FontSize(13).Bold();
            }

            if (section.Rows.Count == 0)
            {
                column.Item().Text(ListMessages.NoData).Italic();
                return;
            }

            column.Item().Element(tableContainer => Table(tableContainer, columns, section.Rows));
        });

    private static void Table(
        IContainer container, IReadOnlyList<ListColumn> columns, IReadOnlyList<IReadOnlyList<object?>> rows) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                foreach (var column in columns)
                {
                    definition.RelativeColumn(ColumnWeight(column.Kind));
                }
            });

            table.Header(header =>
            {
                for (var index = 0; index < columns.Count; index++)
                {
                    Th(header, columns[index], isFirstColumn: index == 0);
                }
            });

            foreach (var row in rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    Td(
                        table, ListCellValues.ToDisplayText(row[index], columns[index].Kind), columns[index].Kind,
                        isFirstColumn: index == 0);
                }
            }
        });

    /// <summary>
    /// How much of the row a column gets, relative to the others — a "Pos."
    /// or a "Dif." never needs more than a couple of digits, and giving it
    /// the same share as a team's name (the same mistake an equal
    /// <c>RelativeColumn()</c> on every column made) leaves the one column
    /// that actually needs the room squeezed and every short one bloated
    /// around two characters.
    /// </summary>
    // internal, not private: testable directly without rendering a PDF, the
    // same convention Athletes.ReadAthletes.ListAsync already uses for logic
    // worth pinning on its own.
    internal static float ColumnWeight(ListValueKind kind) => kind switch
    {
        ListValueKind.Number => 1f,
        ListValueKind.Boolean => 1f,
        ListValueKind.Date => 1.3f,
        ListValueKind.DateTime => 1.8f,
        _ => 2.5f,
    };

    // internal, not private: it is AlignmentFor's return type, which needs
    // to be just as visible as that method itself.
    internal enum CellAlignment
    {
        Left,
        Center,
        Right,
    }

    /// <summary>
    /// Where a column's values sit — a number reads by its last digit, a
    /// label does not. Decided separately from <see cref="Apply"/> so the
    /// decision itself is testable without rendering a PDF, and shared by
    /// <see cref="Th"/> and <see cref="Td"/> so a header can never land
    /// aligned one way while its own column's data sits another.
    /// </summary>
    /// <remarks>
    /// The first column is the one exception: every provider that has a
    /// numeric one there uses it for a row's position, not a quantity —
    /// "Pos." reads as which row this is, not something worth lining up by
    /// magnitude the way a "Pts." column is. Right-aligned, it sits pinned
    /// against the inner border with nothing but empty space ahead of it,
    /// which reads as a mistake rather than a number — a rank is centered in
    /// its column instead, the way every standings table already shows one.
    /// </remarks>
    // internal, not private: testable directly without rendering a PDF, the
    // same convention ColumnWeight above already uses.
    internal static CellAlignment AlignmentFor(ListValueKind kind, bool isFirstColumn)
    {
        if (isFirstColumn && kind == ListValueKind.Number)
        {
            return CellAlignment.Center;
        }

        return kind switch
        {
            ListValueKind.Number => CellAlignment.Right,
            ListValueKind.Boolean => CellAlignment.Center,
            _ => CellAlignment.Left,
        };
    }

    private static IContainer Apply(IContainer cell, CellAlignment alignment) => alignment switch
    {
        CellAlignment.Right => cell.AlignRight(),
        CellAlignment.Center => cell.AlignCenter(),
        _ => cell,
    };

    // PaddingHorizontal on every cell, not just a vertical rule between rows:
    // without it, a right-aligned column followed by a left-aligned one has
    // nothing keeping them apart — both texts sit flush against the exact
    // same boundary ("Pos." then "Equipo" reading as "Pos.Equipo", "1" then
    // "Bayern Munich" as "1Bayern Munich") — the gap between two
    // right-aligned numbers came for free from their own leftover column
    // width, so this never showed before a text column actually followed one.
    // TeamReportPdf's own table cells already carry this same
    // PaddingHorizontal, for what is presumably the same reason.
    private static void Th(TableCellDescriptor header, ListColumn column, bool isFirstColumn) =>
        Apply(
                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2).PaddingHorizontal(4),
                AlignmentFor(column.Kind, isFirstColumn))
            .Text(column.Header).Bold();

    private static void Td(TableDescriptor table, string text, ListValueKind kind, bool isFirstColumn) =>
        Apply(
                table.Cell().PaddingVertical(2).PaddingHorizontal(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2),
                AlignmentFor(kind, isFirstColumn))
            .Text(text);
}
