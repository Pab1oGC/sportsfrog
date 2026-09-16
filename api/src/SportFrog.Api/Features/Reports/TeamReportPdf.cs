using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Reports;

/// <summary>Draws a team's report as a flowing A4 document.</summary>
internal static class TeamReportPdf
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

    public static byte[] Render(TeamReport report) =>
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

    private static void Header(IContainer container, TeamReport report)
    {
        var accent = Accent(report.AccentColor);

        container.Background(Colors.Grey.Lighten5).CornerRadius(8).BorderLeft(4).BorderColor(accent)
            .Padding(12).Row(row =>
        {
            if (report.LogoBytes is { } logo)
            {
                row.ConstantItem(44).Height(44).Image(logo).FitArea();
                row.ConstantItem(12);
            }

            row.RelativeItem().Column(column =>
            {
                column.Item().Text(report.TeamName).FontSize(19).Bold();
                column.Item().Text(report.ClubName is { Length: > 0 } club ? $"{club} — Reporte de equipo" : "Reporte de equipo")
                    .FontSize(12).FontColor(accent);
                column.Item().PaddingTop(2).Text($"{report.CompetitionName}  ·  {report.CategoryName}  ·  {report.SportName}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Body(IContainer container, TeamReport report) =>
        container.PaddingTop(14).Column(column =>
        {
            column.Spacing(16);

            var accent = Accent(report.AccentColor);

            column.Item().Element(item =>
            {
                if (report.IsJudged)
                {
                    Classification(item, report, accent);
                }
                else
                {
                    Standing(item, report, accent);
                }
            });

            if (!report.IsJudged)
            {
                column.Item().Element(item => Matches(item, report.Matches, accent));
                column.Item().Element(item => Leaders(item, report.Leaders, accent));
            }
        });

    private static void Standing(IContainer container, TeamReport report, Color accent)
    {
        if (report.Standing is not { } standing || report.StandingsPosition is not { } position)
        {
            container.Text("Todavía no tiene partidos que cuenten para la tabla.").Italic();
            return;
        }

        container.Column(column =>
        {
            column.Item().Text($"Posición {position}º{(standing.GroupLabel is { } g ? $" — Grupo {g}" : "")}")
                .FontSize(13).Bold();

            column.Item().PaddingTop(8).Row(row =>
            {
                row.Spacing(6);
                StatTile(row, "PJ", standing.Played.ToString(), accent);
                StatTile(row, "G", standing.Won.ToString(), Colors.Green.Darken1);
                StatTile(row, "E", standing.Drawn.ToString(), Colors.Grey.Darken3);
                StatTile(row, "P", standing.Lost.ToString(), Colors.Red.Darken1);
                StatTile(row, "GF-GC", $"{standing.ScoreFor}-{standing.ScoreAgainst}", accent);
                StatTile(row, "Dif.", $"{(standing.ScoreDifference >= 0 ? "+" : "")}{standing.ScoreDifference}", accent);
                StatTile(row, "Puntos", standing.Points.ToString(), accent);
            });
        });
    }

    private static void Classification(IContainer container, TeamReport report, Color accent)
    {
        if (report.Score is null)
        {
            container.Text("Todavía no tiene puntaje cargado en esta clasificación.").Italic();
            return;
        }

        var position = report.ClassificationPosition is { } p ? $"{p}º" : "Sin posición aún";

        container.Row(row =>
        {
            row.Spacing(6);
            StatTile(row, "Clasificación", position, accent);
            StatTile(row, "Puntaje", $"{report.Score.Value / 100.0:0.00}", accent);
            StatTile(row, "Estado", report.PerformanceStatus?.ToString() ?? "—", accent);
        });
    }

    /// <summary>
    /// A small bordered box for one number/label pair — replaces the single
    /// dense text line the summary used to be with something a reader can
    /// scan at a glance instead of parsing.
    /// </summary>
    private static void StatTile(RowDescriptor row, string label, string value, Color accent) =>
        row.RelativeItem().Background(Colors.Grey.Lighten5).CornerRadius(6).Padding(8).Column(column =>
        {
            column.Item().AlignCenter().Text(value).FontSize(15).Bold().FontColor(accent);
            column.Item().AlignCenter().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        });

    private static void Matches(IContainer container, IReadOnlyList<TeamMatchLine> matches, Color accent)
    {
        container.Column(column =>
        {
            column.Item().Text("Partidos").FontSize(13).Bold().FontColor(accent);

            if (matches.Count == 0)
            {
                column.Item().PaddingTop(4).Text("Todavía no tiene partidos cargados.").Italic();
                return;
            }

            column.Item().PaddingTop(6).Table(table =>
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
                    // Fila clara/oscura alternada: una tabla larga de puro texto
                    // se pierde de vista renglón a renglón sin esta guía.
                    var zebra = i % 2 == 1 ? Colors.Grey.Lighten5 : Colors.White;

                    Td(table, zebra, match.ScheduledAt is { } at ? at.ToLocalTime().ToString("dd/MM/yyyy") : "Sin fecha");
                    Td(table, zebra, match.OpponentName);
                    Td(table, zebra, match.Home ? "Local" : "Visitante");
                    Td(table, zebra, match.OwnTotal is { } own && match.OpponentTotal is { } opp ? $"{own} - {opp}" : "vs");
                    OutcomeCell(table, zebra, match.Outcome, match.Status);
                }
            });
        });
    }

    private static void Leaders(IContainer container, IReadOnlyList<TeamLeaderLine> leaders, Color accent)
    {
        if (leaders.Count == 0)
        {
            return;
        }

        container.Column(column =>
        {
            column.Item().Text("Figuras del equipo").FontSize(13).Bold().FontColor(accent);

            column.Item().PaddingTop(6).Column(inner =>
            {
                inner.Spacing(4);

                foreach (var leader in leaders)
                {
                    var jersey = leader.JerseyNumber is { } n ? $" #{n}" : "";

                    inner.Item().Background(Colors.Grey.Lighten5).CornerRadius(4).BorderLeft(3).BorderColor(accent)
                        .Padding(6).Row(row =>
                        {
                            row.RelativeItem(2).Text(leader.MetricLabel).FontColor(Colors.Grey.Darken2);
                            row.RelativeItem(3).Text($"{leader.AthleteName}{jersey}").Bold();
                            row.ConstantItem(40).AlignRight().Text(leader.Total.ToString()).Bold().FontColor(accent);
                        });
                }
            });
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
    /// The competition's own colour, or the blue this report always used
    /// before a competition could set one — never bare, so a competition
    /// that never dressed up its page reads exactly as it always has.
    /// </summary>
    private static Color Accent(string? hex) => string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;
}
