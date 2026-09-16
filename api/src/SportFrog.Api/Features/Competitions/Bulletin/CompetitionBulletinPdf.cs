using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SportFrog.Api.Features.Competitions.Bulletin;

/// <summary>
/// Draws a competition's bulletin as a flowing A4 document.
/// </summary>
/// <remarks>
/// Unlike <see cref="Documents.DocumentRenderer"/>'s absolutely-positioned
/// card faces, this is QuestPDF's ordinary flowing layout: a bulletin is
/// prose and tables of unpredictable length — a category list that might be
/// three rows or thirty, sanctions that might be one paragraph or five — and
/// nothing here can know in advance how many pages that becomes. Flowing
/// content is what lets QuestPDF work that out instead of this code guessing
/// at it.
/// </remarks>
internal static class CompetitionBulletinPdf
{
    public static byte[] Render(BulletinData data) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Element(container => Header(container, data));
            page.Content().Element(container => Body(container, data));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .GeneratePdf();

    private static void Header(IContainer container, BulletinData data) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (data.LogoBytes is { } logo)
                {
                    row.ConstantItem(48).Height(48).Image(logo).FitArea();
                    row.ConstantItem(12);
                }

                row.RelativeItem().Column(text =>
                {
                    text.Item().Text(data.OrganizationName).FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.Item().Text(data.CompetitionName).FontSize(18).Bold();
                    text.Item().Text("Convocatoria").FontSize(12).FontColor(Accent(data.AccentColor));
                });
            });

            column.Item().PaddingTop(4).Text(Subtitle(data));
            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

    private static string Subtitle(BulletinData data)
    {
        var parts = new List<string> { data.SportName, $"Temporada {data.Season}", data.FormatLabel };

        if (data.StartsOn is { } from)
        {
            parts.Add(data.EndsOn is { } to
                ? $"Del {from:dd/MM/yyyy} al {to:dd/MM/yyyy}"
                : $"Desde el {from:dd/MM/yyyy}");
        }

        return string.Join("  ·  ", parts);
    }

    private static void Body(IContainer container, BulletinData data) =>
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(14);

            if (data.Introduction is { Length: > 0 } introduction)
            {
                column.Item().Element(item => Section(item, "Presentación", introduction, data.AccentColor));
            }

            column.Item().Element(item => Categories(item, data.Categories, data.AccentColor));
            column.Item().Element(item => Regulations(item, data.Categories, data.AccentColor));

            if (data.Sanctions is { Length: > 0 } sanctions)
            {
                column.Item().Element(item => Section(item, "Sanciones", sanctions, data.AccentColor));
            }

            if (data.GeneralProvisions is { Length: > 0 } provisions)
            {
                column.Item().Element(item => Section(item, "Disposiciones generales", provisions, data.AccentColor));
            }

            if (data.ContactInfo is { Length: > 0 } contact)
            {
                column.Item().Element(item => Section(item, "Contacto", contact, data.AccentColor));
            }
        });

    private static void Heading(IContainer container, string title, string? accentColor) =>
        container.Text(title).FontSize(13).Bold().FontColor(Accent(accentColor));

    /// <summary>A block of the organizer's own prose, under a heading.</summary>
    private static void Section(IContainer container, string title, string body, string? accentColor) =>
        container.Column(column =>
        {
            column.Item().Element(item => Heading(item, title, accentColor));

            // Split on blank lines rather than printed as one block: prose
            // typed into a plain-text field carries its own paragraph breaks,
            // and QuestPDF's Text does not honour a bare "\n\n" as one.
            foreach (var paragraph in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                column.Item().PaddingTop(4).Text(paragraph.Trim());
            }
        });

    /// <summary>Who may enter each category — the table a delegate checks a roster against.</summary>
    private static void Categories(IContainer container, IReadOnlyList<BulletinCategory> categories, string? accentColor) =>
        container.Column(column =>
        {
            column.Item().Element(item => Heading(item, "Categorías", accentColor));

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    Th(header, "Categoría");
                    Th(header, "Género");
                    Th(header, "Edad");
                    Th(header, "Peso");
                    Th(header, "Cupo");
                });

                foreach (var category in categories)
                {
                    Td(table, category.Name);
                    Td(table, category.Gender ?? "Abierto");
                    Td(table, category.AgeRange ?? "Sin restricción");
                    Td(table, category.WeightRange ?? "Sin restricción");
                    Td(table, category.MaxRosterSize is { } max ? max.ToString() : "Sin límite");
                }
            });
        });

    /// <summary>How each category plays and how a match is decided.</summary>
    private static void Regulations(IContainer container, IReadOnlyList<BulletinCategory> categories, string? accentColor) =>
        container.Column(column =>
        {
            column.Item().Element(item => Heading(item, "Reglamento y puntaje", accentColor));
            column.Spacing(8);

            foreach (var category in categories)
            {
                column.Item().PaddingTop(6).Column(inner =>
                {
                    inner.Item().Text(category.Name).Bold();
                    inner.Item().Text(category.PeriodsSummary);
                    inner.Item().Text($"Resultados: {string.Join(", ", category.Outcomes)}.");

                    if (category.Tiebreakers.Count > 0)
                    {
                        inner.Item().Text($"Desempate, en orden: {string.Join(" → ", category.Tiebreakers)}.");
                    }

                    if (category.IsJudged)
                    {
                        inner.Item().Text(
                            "Corre además una etapa de clasificación previa, ordenada por puntaje "
                                + "de los jueces, antes de sortear la eliminatoria.");
                    }
                });
            }
        });

    private static void Th(TableCellDescriptor header, string text) =>
        header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2)
            .Text(text).Bold();

    private static void Td(TableDescriptor table, string text) =>
        table.Cell().PaddingVertical(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(text);

    /// <summary>
    /// The competition's own colour, or the blue this bulletin always used
    /// before a competition could set one.
    /// </summary>
    private static Color Accent(string? hex) => string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;
}
