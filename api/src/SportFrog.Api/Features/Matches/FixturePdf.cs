using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Matches;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Draws a calendar of fixtures — a whole competition's or one category's —
/// as a flowing A4 document.
/// </summary>
/// <remarks>
/// The same flowing-layout reasoning as <c>Competitions.Bulletin.CompetitionBulletinPdf</c>:
/// a season can be four matches or four hundred, and nothing here can know
/// which before it starts laying rows out.
///
/// One table, chronological, categories interleaved rather than grouped —
/// the same order <see cref="ReadMatches.Ordered"/> already reads a
/// competition's own calendar in, and for the reason its own remarks give:
/// that is the view an organizer actually works from, because a Sunday has
/// matches of several divisions on the grounds available that day, and
/// grouping by category would hide exactly the collisions between them that
/// a printed sheet is often pulled out to check.
/// </remarks>
internal static class FixturePdf
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

    /// <param name="categoryName">
    /// Named in the header instead of as a column when the whole sheet is
    /// already one category — repeating it on every one of its rows says
    /// nothing a heading has not already said.
    /// </param>
    /// <param name="round">
    /// Named in the header the same way — a sheet asked for one round is
    /// already implicitly one jornada, and every row in it shares the round
    /// its own filter already guaranteed, so there is nothing left to spell
    /// out per row either.
    /// </param>
    /// <param name="branding">The competition's own mark and colour. See <see cref="CompetitionBranding"/>.</param>
    public static byte[] Render(
        string competitionName,
        string? categoryName,
        short? round,
        CompetitionBranding branding,
        IReadOnlyList<ReadMatches.Summary> matches) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().Element(container => Header(container, competitionName, categoryName, round, branding));
            page.Content().Element(container => Body(container, categoryName, matches));
            page.Footer().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }))
        .GeneratePdf();

    /// <summary>Everything a request already narrowed the sheet to, read back as one line.</summary>
    private static string Subtitle(string? categoryName, short? round) => (categoryName, round) switch
    {
        (null, null) => "Fixture",
        (null, { } jornada) => $"Fixture — Jornada {jornada}",
        ({ } category, null) => $"Fixture — {category}",
        ({ } category, { } jornada) => $"Fixture — {category} — Jornada {jornada}",
    };

    private static void Header(
        IContainer container, string competitionName, string? categoryName, short? round, CompetitionBranding branding) =>
        container.Row(row =>
        {
            if (branding.LogoBytes is { } logo)
            {
                row.ConstantItem(36).Height(36).Image(logo).FitArea();
                row.ConstantItem(10);
            }

            row.RelativeItem().Column(column =>
            {
                column.Item().Text(competitionName).FontSize(18).Bold();
                column.Item().Text(Subtitle(categoryName, round))
                    .FontSize(12).FontColor(Accent(branding.AccentColor));
                column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
        });

    private static void Body(
        IContainer container, string? categoryName, IReadOnlyList<ReadMatches.Summary> matches)
    {
        if (matches.Count == 0)
        {
            container.PaddingTop(10).Text("Todavía no hay partidos cargados.").Italic();
            return;
        }

        var showCategory = categoryName is null;

        container.PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.6f); // Fecha

                if (showCategory)
                {
                    columns.RelativeColumn(1.4f); // Categoría
                }

                columns.RelativeColumn(2.2f); // Local
                columns.RelativeColumn(0.8f); // Marcador
                columns.RelativeColumn(2.2f); // Visitante
                columns.RelativeColumn(1.8f); // Cancha
                columns.RelativeColumn(1.2f); // Estado
            });

            table.Header(header =>
            {
                Th(header, "Fecha");

                if (showCategory)
                {
                    Th(header, "Categoría");
                }

                Th(header, "Local");
                Th(header, "");
                Th(header, "Visitante");
                Th(header, "Cancha");
                Th(header, "Estado");
            });

            foreach (var match in matches)
            {
                Td(table, match.ScheduledAt is { } at ? at.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Sin fecha");

                if (showCategory)
                {
                    Td(table, match.CategoryName);
                }

                Td(table, match.HomeTeamName);
                Td(table, Score(match));
                Td(table, match.AwayTeamName);
                Td(table, match.VenueName is { } venue ? $"{venue} — {match.SpaceName}" : "Sin definir");
                Td(table, StatusLabels.GetValueOrDefault(match.Status, match.Status.ToString()));
            }
        });
    }

    private static string Score(ReadMatches.Summary match) =>
        match.HomeTotal is { } home && match.AwayTotal is { } away ? $"{home} - {away}" : "vs";

    private static void Th(TableCellDescriptor header, string text) =>
        header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2).Text(text).Bold();

    private static void Td(TableDescriptor table, string text) =>
        table.Cell().PaddingVertical(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(text);

    /// <summary>
    /// The competition's own colour, or the blue this sheet always used
    /// before a competition could set one.
    /// </summary>
    private static Color Accent(string? hex) => string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;
}
