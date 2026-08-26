using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Api.Infrastructure.Persistence.Entities;
// QuestPDF also has a PageSize. Here the word always means the one from the
// design catalogue: the size the card is laid out for, in millimetres.
using PageSize = SportFrog.Domain.Documents.PageSize;

namespace SportFrog.Api.Features.Documents;

/// <summary>The pictures a card needs, fetched before anything is drawn.</summary>
/// <remarks>
/// Passed in rather than fetched here because object storage is slow and the
/// same artwork is on every card of the batch. Reading it once and drawing it
/// four hundred times is the difference between a batch and an afternoon.
/// </remarks>
internal sealed record DocumentAssets(
    byte[]? FrontBackground,
    byte[]? BackBackground,
    byte[]? Photo,
    byte[]? Qr);

/// <summary>
/// Draws a design onto a page.
/// </summary>
/// <remarks>
/// Everything a layout says is a fraction of the face, and everything a PDF
/// wants is a length. This file is where the one becomes the other, and it is
/// the only place that knows how: nothing else multiplies a coordinate by a
/// page size.
///
/// Positioning is done with layers and padding rather than a canvas, so the
/// text stays text. A credential drawn as an image would print acceptably at
/// card size and badly on an A4 certificate, and would be unsearchable and
/// unselectable in every PDF reader — which matters for a document somebody is
/// sent by email rather than handed on a lanyard.
/// </remarks>
internal static class DocumentRenderer
{
    /// <summary>Points per millimetre, which is how PDF measures things.</summary>
    private const float PointsPerMillimetre = 72f / 25.4f;

    /// <summary>
    /// Rough width of a glyph as a fraction of the font size.
    /// </summary>
    /// <remarks>
    /// Used only to decide how far <c>shrink</c> has to shrink. The typefaces
    /// here are proportional, so this is an average and not a measurement —
    /// good enough to keep a long surname inside its box, and deliberately a
    /// little pessimistic so the answer errs towards smaller rather than
    /// towards overflowing.
    /// </remarks>
    private const double AverageGlyphWidth = 0.52;

    /// <summary>Composes one document as its own PDF.</summary>
    public static byte[] Render(
        TemplateLayout layout,
        string pageSize,
        DocumentPrint print,
        DocumentAssets assets)
    {
        var page = TemplateDesign.PageSizes.First(size => size.Code == pageSize);

        return Document.Create(document =>
        {
            Compose(document, page, layout.Front, print, assets, assets.FrontBackground);

            if (layout.Back is { } back)
            {
                Compose(document, page, back, print, assets, assets.BackBackground);
            }
        })
        .GeneratePdf();
    }

    /// <summary>Lays one face onto one page of the document.</summary>
    private static void Compose(
        IDocumentContainer document,
        PageSize size,
        TemplateFace face,
        DocumentPrint print,
        DocumentAssets assets,
        byte[]? background) =>
        document.Page(page =>
        {
            page.Size((float)size.Width, (float)size.Height, Unit.Millimetre);

            // No margin, ever. A credential is designed to the edge of the
            // card: the artwork bleeds and the fields are placed against the
            // whole face, which is what the fractions in the layout mean.
            page.Margin(0);
            page.PageColor(Colors.White);

            Draw(page.Content(), face, size, print, assets, background);
        });

    /// <summary>
    /// Draws a face into whatever container is given.
    /// </summary>
    /// <remarks>
    /// Shared with the print sheet, which places the same face into a small
    /// box on a big page rather than onto a page of its own. Everything is a
    /// fraction of the card, so the same code produces the same card at either
    /// scale — which is the property that makes the sheet trustworthy: what is
    /// cut out is what was sent to the person individually.
    /// </remarks>
    public static void Draw(
        IContainer container,
        TemplateFace face,
        PageSize size,
        DocumentPrint print,
        DocumentAssets assets,
        byte[]? background) =>
        container.Layers(layers =>
        {
            layers.PrimaryLayer().Extend().Element(behind =>
            {
                if (background is not null)
                {
                    // Stretched rather than fitted. The artwork was uploaded
                    // for this card and the editor laid the fields out over
                    // it; letterboxing it here would move every field relative
                    // to the picture they were positioned against.
                    behind.Image(background).FitUnproportionally();
                }
            });

            foreach (var field in face.Fields)
            {
                Place(layers.Layer(), field, size, print, assets);
            }
        });

    /// <summary>Puts one field where the layout says it goes.</summary>
    private static void Place(
        IContainer layer,
        TemplateField field,
        PageSize size,
        DocumentPrint print,
        DocumentAssets assets)
    {
        var width = (float)size.Width;
        var height = (float)size.Height;

        // Padding from the top-left corner, with the element pinned to that
        // corner, is absolute positioning: the padding is the coordinate.
        var placed = layer
            .AlignLeft()
            .AlignTop()
            .PaddingLeft((float)field.X * width, Unit.Millimetre)
            .PaddingTop((float)field.Y * height, Unit.Millimetre);

        if (TemplateDesign.Source(field.Source) is not { } source)
        {
            return;
        }

        if (source.Shape == FieldShape.Image)
        {
            var picture = field.Source switch
            {
                "athlete.photo" => assets.Photo,
                "document.qr" => assets.Qr,
                _ => null,
            };

            if (picture is null)
            {
                return;
            }

            placed
                .Width((float)(field.W ?? 0) * width, Unit.Millimetre)
                .Height((float)(field.H ?? 0) * height, Unit.Millimetre)

                // Contained rather than stretched: a portrait squeezed into a
                // square box is a distorted face, and the face is the whole
                // point of the field.
                .Image(picture).FitArea();

            return;
        }

        var words = field.Source == "text" ? field.Text : DocumentValues.Text(field.Source, print);

        if (string.IsNullOrWhiteSpace(words))
        {
            return;
        }

        // The box the words live in. A designer draws one; where they did not,
        // it runs to the right edge, which is what a left-aligned field wants.
        var boxWidth = (float)(field.W ?? (1 - field.X)) * width;
        var points = (float)((field.Size ?? 0.06) * height * PointsPerMillimetre);
        var fit = field.Fit ?? TemplateDesign.DefaultFit;

        if (fit == "shrink")
        {
            points = Shrink(words, points, boxWidth, (float)((field.MinSize ?? 0) * height));
        }

        placed.Width(boxWidth, Unit.Millimetre).Text(text =>
        {
            text.DefaultTextStyle(style => Style(style, field, points));

            switch (field.Align ?? TemplateDesign.DefaultAlign)
            {
                case "center": text.AlignCenter(); break;
                case "right": text.AlignRight(); break;
                default: text.AlignLeft(); break;
            }

            if (fit != "wrap")
            {
                // One line, and an ellipsis if it still will not fit. Wrapping
                // is the only fit that may take a second line: a credential
                // laid out to the millimetre has nowhere to put one.
                text.ClampLines(1, "…");
            }

            text.Span(words);
        });
    }

    private static TextStyle Style(TextStyle style, TemplateField field, float points)
    {
        var font = TemplateDesign.Fonts.FirstOrDefault(candidate =>
            candidate.Code == (field.Font ?? TemplateDesign.DefaultFont));

        style = style
            .FontSize(points)

            // The chain matters. The first name is what the design asked for;
            // Lato is what QuestPDF carries inside itself, so the last resort
            // is always present even on a container with no fonts installed.
            .FontFamily(font?.Rendered ?? "Lato", "Lato");

        if (field.Bold == true)
        {
            style = style.Bold();
        }

        return field.Color is { } color ? style.FontColor(color) : style;
    }

    /// <summary>
    /// How small the words have to get to stay on one line.
    /// </summary>
    /// <remarks>
    /// Estimated rather than measured, because the layout engine will not
    /// measure text until it draws it and by then the size is decided.
    /// Stepping down in tenths converges in a few iterations and stops at the
    /// floor the designer set — below which a name is unreadable anyway, and
    /// clipping it is the more honest outcome than shrinking it to nothing.
    /// </remarks>
    private static float Shrink(string words, float points, float boxWidthMm, float minimumMm)
    {
        var boxWidthPoints = boxWidthMm * PointsPerMillimetre;
        var floor = Math.Max(minimumMm * PointsPerMillimetre, 1f);

        while (points > floor && words.Length * points * AverageGlyphWidth > boxWidthPoints)
        {
            points *= 0.9f;
        }

        return Math.Max(points, floor);
    }
}
