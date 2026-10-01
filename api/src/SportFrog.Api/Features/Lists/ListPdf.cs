using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SportFrog.Api.Features.Lists;

/// <summary>Renders a <see cref="ListTable"/> to a flowing PDF document.</summary>
/// <remarks>
/// The same flowing-layout and <c>Th</c>/<c>Td</c> shape
/// <c>Matches.FixturePdf</c> and the other report renderers already use —
/// a list can be ten rows or ten thousand, and nothing here can know which
/// before it starts laying them out. Unlike those, this one never reads a
/// branding or a photo: it draws only what <see cref="ListTable"/> itself
/// carries, so a slow image link can never be the reason an export hangs.
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

    public static byte[] Render(ListTable table) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(table.Columns.Count > ColumnsThatForceLandscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().Element(container => Header(container, table));
            page.Content().Element(container => Body(container, table));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .GeneratePdf();

    private static void Header(IContainer container, ListTable table) =>
        container.Column(column =>
        {
            column.Item().Text(table.Title).FontSize(18).Bold();

            if (table.Subtitle is { } subtitle)
            {
                column.Item().Text(subtitle).FontSize(12).FontColor(Colors.Grey.Darken2);
            }

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

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
                foreach (var unused in columns)
                {
                    definition.RelativeColumn();
                }
            });

            table.Header(header =>
            {
                foreach (var column in columns)
                {
                    Th(header, column.Header);
                }
            });

            foreach (var row in rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    Td(table, ListCellValues.ToDisplayText(row[index], columns[index].Kind), columns[index].Kind);
                }
            }
        });

    private static void Th(TableCellDescriptor header, string text) =>
        header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2).Text(text).Bold();

    private static void Td(TableDescriptor table, string text, ListValueKind kind)
    {
        var cell = table.Cell().PaddingVertical(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

        // Right-aligned the way TeamReportPdf already right-aligns a
        // leader's total — a number reads by its last digit, a label does
        // not.
        (kind == ListValueKind.Number ? cell.AlignRight() : cell).Text(text);
    }
}
