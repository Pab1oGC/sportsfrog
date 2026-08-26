using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Api.Infrastructure.Persistence.Entities;
// QuestPDF also has a PageSize. Here the word always means the one from the
// design catalogue: the size the card is laid out for, in millimetres.
using PageSize = SportFrog.Domain.Documents.PageSize;

namespace SportFrog.Api.Features.Documents;

/// <summary>One card, already drawn, waiting to be placed on a sheet.</summary>
internal sealed record SheetCard(TemplateFace Face, DocumentPrint Print, DocumentAssets Assets);

/// <summary>
/// Lays a batch of cards out on paper the way a printer wants them.
/// </summary>
/// <remarks>
/// This is the file that makes a batch usable. Four hundred one-card PDFs are
/// four hundred print jobs on card-sized paper nobody has; what an operator
/// actually does is print ten to an A4 sheet and cut them apart. The
/// individual documents still exist — they are for sending one person their
/// own credential — but the sheet is what goes to the printer.
///
/// Only for cards small enough to gang up. A certificate is already A4: laying
/// two of those on a page would mean printing them at half size, which is not
/// an imposition, it is a mistake.
/// </remarks>
internal static class PrintSheet
{
    /// <summary>A4, which is what the printer in the club office has.</summary>
    private const float SheetWidth = 210f;
    private const float SheetHeight = 297f;

    /// <summary>
    /// Kept clear of the edges, because almost no office printer reaches them
    /// and a card that lands in the unprintable margin comes out cropped.
    /// </summary>
    private const float Margin = 10f;

    /// <summary>
    /// The gap between cards, which is also where the blade goes. Wide enough
    /// that a slightly crooked cut takes the gap rather than the artwork.
    /// </summary>
    private const float Gutter = 4f;

    /// <summary>How long the corner marks are.</summary>
    private const float MarkLength = 3f;

    /// <summary>
    /// Whether a page size is worth ganging up at all.
    /// </summary>
    /// <remarks>
    /// Two cards across an A4 is the threshold. Anything that does not manage
    /// that is already a sheet of paper, and the honest answer for a batch of
    /// those is the documents themselves.
    /// </remarks>
    public static bool Fits(PageSize card) =>
        card.Width * 2 + Gutter + (Margin * 2) <= SheetWidth
        && card.Height * 2 + Gutter + (Margin * 2) <= SheetHeight;

    /// <summary>Composes every card of a batch onto as many sheets as it takes.</summary>
    public static byte[] Render(PageSize card, IReadOnlyList<SheetCard> cards)
    {
        var columns = Math.Max(1, (int)((SheetWidth - (Margin * 2) + Gutter)
            / ((float)card.Width + Gutter)));
        var rows = Math.Max(1, (int)((SheetHeight - (Margin * 2) + Gutter)
            / ((float)card.Height + Gutter)));

        var perSheet = columns * rows;

        return Document.Create(document =>
        {
            for (var start = 0; start < cards.Count; start += perSheet)
            {
                var sheet = cards.Skip(start).Take(perSheet).ToList();

                document.Page(page =>
                {
                    page.Size(SheetWidth, SheetHeight, Unit.Millimetre);
                    page.Margin(0);
                    page.PageColor(Colors.White);

                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Extend();

                        for (var index = 0; index < sheet.Count; index++)
                        {
                            var left = Margin + (index % columns) * ((float)card.Width + Gutter);
                            var top = Margin + (index / columns) * ((float)card.Height + Gutter);

                            Cut(layers, left, top, card);
                            Card(layers.Layer(), left, top, card, sheet[index]);
                        }
                    });
                });
            }
        })
        .GeneratePdf();
    }

    /// <summary>Places one card at its spot on the sheet.</summary>
    private static void Card(
        IContainer layer,
        float left,
        float top,
        PageSize card,
        SheetCard placed) =>
        layer
            .AlignLeft()
            .AlignTop()
            .PaddingLeft(left, Unit.Millimetre)
            .PaddingTop(top, Unit.Millimetre)
            .Width((float)card.Width, Unit.Millimetre)
            .Height((float)card.Height, Unit.Millimetre)
            .Element(container => DocumentRenderer.Draw(
                container,
                placed.Face,
                card,
                placed.Print,
                placed.Assets,
                placed.Assets.FrontBackground));

    /// <summary>
    /// The marks somebody cuts along.
    /// </summary>
    /// <remarks>
    /// Drawn just outside each corner rather than as lines through the sheet,
    /// so nothing crosses the artwork and a mark that survives a slightly
    /// crooked cut is still outside the finished card. Hairline, because a
    /// thick mark is a mark you cut in the middle of and guess at.
    /// </remarks>
    private static void Cut(LayersDescriptor layers, float left, float top, PageSize card)
    {
        var width = (float)card.Width;
        var height = (float)card.Height;

        // Four corners, two strokes each: one reaching out sideways and one
        // reaching up or down, so the crossing point is the corner itself
        // without any ink landing on it.
        Mark(left - MarkLength, top, MarkLength, 0.2f);
        Mark(left, top - MarkLength, 0.2f, MarkLength);

        Mark(left + width, top, MarkLength, 0.2f);
        Mark(left + width - 0.2f, top - MarkLength, 0.2f, MarkLength);

        Mark(left - MarkLength, top + height - 0.2f, MarkLength, 0.2f);
        Mark(left, top + height, 0.2f, MarkLength);

        Mark(left + width, top + height - 0.2f, MarkLength, 0.2f);
        Mark(left + width - 0.2f, top + height, 0.2f, MarkLength);

        // A fresh layer per stroke. A container in this library is a place,
        // not a brush: reusing one for the second mark would replace the first
        // rather than add to it.
        void Mark(float x, float y, float w, float h) =>
            layers.Layer()
                .AlignLeft()
                .AlignTop()
                .PaddingLeft(x, Unit.Millimetre)
                .PaddingTop(y, Unit.Millimetre)
                .Width(w, Unit.Millimetre)
                .Height(h, Unit.Millimetre)
                .Background(Colors.Grey.Darken2);
    }
}
