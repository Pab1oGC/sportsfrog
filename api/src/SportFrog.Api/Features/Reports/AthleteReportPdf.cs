using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Reports;

/// <summary>Draws an athlete's file as a flowing A4 document — one section per registration.</summary>
internal static class AthleteReportPdf
{
    private static readonly Dictionary<MatchState, string> StatusLabels = new()
    {
        [MatchState.Scheduled] = "Programado",
        [MatchState.InProgress] = "En curso",
        [MatchState.Finished] = "Finalizado",
        [MatchState.Postponed] = "Aplazado",
        [MatchState.Walkover] = "Walkover",
        [MatchState.Cancelled] = "Cancelado",
    };

    public static byte[] Render(AthleteReport report) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Element(container => Header(container, report));
            page.Content().Element(container => Body(container, report));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .GeneratePdf();

    private static void Header(IContainer container, AthleteReport report) =>
        container.Background(Colors.Grey.Lighten5).CornerRadius(8).BorderLeft(4).BorderColor(Colors.Blue.Darken2)
            .Padding(12).Row(row =>
        {
            // Always drawn, real photograph or placeholder — see
            // AthletePhoto.BytesAsync, which is what guarantees
            // report.PhotoBytes is never itself null or empty.
            row.ConstantItem(60).Height(60).Image(report.PhotoBytes).FitArea();
            row.ConstantItem(12);

            row.RelativeItem().Column(column =>
            {
                column.Item().Text($"{report.FirstName} {report.LastName}").FontSize(19).Bold();
                column.Item().Text("Ficha del deportista").FontSize(12).FontColor(Colors.Blue.Darken2);
                var age = Age(report.BirthDate);
                column.Item().PaddingTop(2).Text(
                    $"Doc. {report.DocumentId}  ·  {report.BirthDate:dd/MM/yyyy} ({age} años)" +
                    (report.Gender is { Length: > 0 } gender ? $"  ·  {gender}" : "") +
                    (report.WeightKg is { } weight ? $"  ·  {weight:0.##} kg" : ""))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

    private static int Age(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;

        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static void Body(IContainer container, AthleteReport report)
    {
        if (report.Entries.Count == 0)
        {
            container.PaddingTop(10).Text("Sin inscripciones registradas.").Italic();
            return;
        }

        container.PaddingTop(14).Column(column =>
        {
            column.Spacing(16);

            foreach (var entry in report.Entries)
            {
                column.Item().Element(item => Entry(item, entry));
            }
        });
    }

    private static void Entry(IContainer container, AthleteEntryReport entry)
    {
        var accent = Accent(entry.AccentColor);

        container.Background(Colors.Grey.Lighten5).CornerRadius(6).Padding(10).Row(row =>
        {
            if (entry.LogoBytes is { } logo)
            {
                row.ConstantItem(32).Height(32).Image(logo).FitArea();
                row.ConstantItem(8);
            }

            row.RelativeItem().Column(column =>
            {
                column.Item().Text($"{entry.TeamName} — {entry.CategoryName}").FontSize(13).Bold().FontColor(accent);
                column.Item().Text(
                    $"{entry.CompetitionName}  ·  {entry.SportName}" + (entry.Withdrawn ? "  ·  Retirado del equipo" : ""))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                if (entry.IsJudged)
                {
                    column.Item().PaddingTop(8).Element(item => Classification(item, entry, accent));
                    return;
                }

                column.Item().PaddingTop(8).Element(item => Metrics(item, entry.Metrics, accent));
                column.Item().PaddingTop(8).Element(item => Matches(item, entry.Matches, accent));
            });
        });
    }

    private static void Classification(IContainer container, AthleteEntryReport entry, Color accent)
    {
        if (entry.Score is null)
        {
            container.Text("Todavía no tiene puntaje cargado en esta clasificación.").Italic();
            return;
        }

        var position = entry.ClassificationPosition is { } p ? $"{p}º" : "Sin posición aún";

        container.Row(row =>
        {
            row.Spacing(6);
            StatTile(row, "Puntaje", $"{entry.Score.Value / 100.0:0.00}", accent);
            StatTile(row, "Clasificación", position, accent);
            StatTile(row, "Estado", entry.PerformanceStatus?.ToString() ?? "—", accent);
        });
    }

    private static void Metrics(IContainer container, IReadOnlyList<AthleteMetricTotal> metrics, Color accent)
    {
        if (metrics.Count == 0)
        {
            container.Text("Sin estadísticas propias registradas todavía.").Italic();
            return;
        }

        container.Row(row =>
        {
            row.Spacing(6);
            foreach (var metric in metrics)
            {
                StatTile(row, metric.MetricLabel, metric.Total.ToString(), accent);
            }
        });
    }

    /// <summary>
    /// A small bordered box for one number/label pair — replaces the single
    /// dense text line these summaries used to be with something a reader
    /// can scan at a glance instead of parsing.
    /// </summary>
    private static void StatTile(RowDescriptor row, string label, string value, Color accent) =>
        row.RelativeItem().Background(Colors.White).CornerRadius(6).Padding(8).Column(column =>
        {
            column.Item().AlignCenter().Text(value).FontSize(14).Bold().FontColor(accent);
            column.Item().AlignCenter().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        });

    private static void Matches(IContainer container, IReadOnlyList<AthleteMatchLine> matches, Color accent)
    {
        if (matches.Count == 0)
        {
            container.Text("Su equipo todavía no tiene partidos cargados.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.3f);
            });

            table.Header(header =>
            {
                Th(header, "Fecha");
                Th(header, "Rival");
                Th(header, "Local/Vis.");
                Th(header, "Marcador");
                Th(header, "Resultado");
            });

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var zebra = i % 2 == 1 ? Colors.Grey.Lighten4 : Colors.White;

                Td(table, zebra, match.ScheduledAt is { } at ? at.ToLocalTime().ToString("dd/MM/yyyy") : "Sin fecha");
                Td(table, zebra, match.OpponentName);
                Td(table, zebra, match.Home ? "Local" : "Visitante");
                Td(table, zebra, match.OwnTotal is { } own && match.OpponentTotal is { } opp ? $"{own} - {opp}" : "vs");
                OutcomeCell(table, zebra, match.Outcome, match.Status);
            }
        });
    }

    private static void Th(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Darken1)
            .Padding(4).Text(text).Bold();

    private static void Td(TableDescriptor table, Color background, string text) =>
        table.Cell().Background(background).PaddingVertical(3).PaddingHorizontal(2)
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(text);

    /// <summary>
    /// The result, as a small colour-coded badge instead of plain text — the
    /// one column a reader actually scans the whole table for.
    /// </summary>
    private static void OutcomeCell(TableDescriptor table, Color zebra, string? outcome, MatchState status)
    {
        var label = outcome ?? StatusLabels.GetValueOrDefault(status, status.ToString());
        var (background, text) = OutcomeColor(outcome, status);

        table.Cell().Background(zebra).PaddingVertical(3).PaddingHorizontal(2)
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Row(row =>
            {
                row.AutoItem().Background(background).CornerRadius(4).Padding(4)
                    .Text(label).FontSize(8.5f).Bold().FontColor(text);
            });
    }

    private static (Color Background, Color Text) OutcomeColor(string? outcome, MatchState status) => outcome switch
    {
        "Ganado" => (Colors.Green.Lighten4, Colors.Green.Darken3),
        "Perdido" => (Colors.Red.Lighten4, Colors.Red.Darken3),
        "Empatado" => (Colors.Grey.Lighten2, Colors.Grey.Darken3),
        _ => status switch
        {
            MatchState.Cancelled => (Colors.Grey.Lighten3, Colors.Grey.Darken2),
            MatchState.Walkover => (Colors.Orange.Lighten4, Colors.Orange.Darken3),
            _ => (Colors.Blue.Lighten4, Colors.Blue.Darken3),
        },
    };

    /// <summary>
    /// A registration's own competition colour, or the blue this report
    /// always used before a competition could set one.
    /// </summary>
    private static Color Accent(string? hex) => string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;
}
