using System.Text.Json.Serialization;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// How a column's values should be typed by a renderer. A score column typed
/// as <see cref="Number"/> is something Excel can sum; typed as
/// <see cref="Text"/> it is a label that happens to look like one — the
/// difference a spreadsheet cannot recover on its own once the cell is
/// written.
/// </summary>
/// <remarks>
/// Also what a JSON preview's own columns carry, so a caller rendering one
/// without ever touching <see cref="ListXlsx"/> or <see cref="ListPdf"/>
/// still knows whether a cell is a number to align right or a label to
/// leave alone.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<ListValueKind>))]
internal enum ListValueKind
{
    Text,
    Number,
    Date,
    DateTime,
    Boolean,
}

/// <summary>One column of a list, as a renderer needs to know it.</summary>
internal sealed record ListColumn(string Header, ListValueKind Kind);

/// <summary>
/// One named block of rows inside a list — a metric's own board, a group's
/// own table in the standings. A list with nothing to group by still has one
/// section, not a special case without one: an Excel renderer turns each
/// section into a sheet, a PDF renderer into a labeled block, and neither has
/// to ask whether there is one section or several.
/// </summary>
/// <param name="Label">
/// What the section is called — "Goles", "Grupo A". Null for a list that is
/// already whole without a name of its own.
/// </param>
/// <param name="Rows">
/// Each row carries one value per <see cref="ListTable.Columns"/>, in the
/// same order. A cell's runtime type must agree with its column's
/// <see cref="ListColumn.Kind"/>, since that is what a renderer trusts
/// instead of inspecting the value itself.
/// </param>
internal sealed record ListSection(string? Label, IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>
/// A list, shaped for export but committed to neither Excel nor PDF — the
/// one thing both renderers read, so a third format a provider never heard
/// of is only ever a third renderer.
/// </summary>
/// <remarks>
/// Every row is checked against <see cref="Columns"/> at construction. A
/// provider that writes a four-value row for a five-column table has a bug
/// worth failing loudly on here, rather than one a renderer discovers later
/// as a misaligned cell nobody can explain.
/// </remarks>
internal sealed record ListTable
{
    public ListTable(
        string title,
        string? subtitle,
        IReadOnlyList<ListColumn> columns,
        IReadOnlyList<ListSection> sections)
    {
        foreach (var section in sections)
        {
            foreach (var row in section.Rows)
            {
                if (row.Count != columns.Count)
                {
                    throw new ArgumentException(
                        $"La fila tiene {row.Count} valores pero la lista declara {columns.Count} columnas.",
                        nameof(sections));
                }
            }
        }

        Title = title;
        Subtitle = subtitle;
        Columns = columns;
        Sections = sections;
    }

    /// <summary>What the list is called — shown as the export's heading.</summary>
    public string Title { get; }

    /// <summary>The list's scope in plain language — a category, a date range. Null when the title already says it.</summary>
    public string? Subtitle { get; }

    public IReadOnlyList<ListColumn> Columns { get; }

    public IReadOnlyList<ListSection> Sections { get; }
}
