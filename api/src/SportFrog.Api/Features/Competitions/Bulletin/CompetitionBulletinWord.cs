using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SportFrog.Api.Features.Competitions.Bulletin;

/// <summary>
/// Draws the same bulletin <see cref="CompetitionBulletinPdf"/> draws, as an
/// editable Word document.
/// </summary>
/// <remarks>
/// A second renderer rather than a converted PDF, because that is what the
/// format is actually for here: an organizer who wants to tweak a sentence
/// of the bulletin their federation already reviewed should not have to come
/// back to this system to do it. The two renderers read the exact same
/// <see cref="BulletinData"/>, so what a PDF and a Word copy of the same
/// bulletin say never has a chance to disagree.
/// </remarks>
internal static class CompetitionBulletinWord
{
    public static byte[] Render(BulletinData data)
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            Header(mainPart, body, data);

            if (data.Introduction is { Length: > 0 } introduction)
            {
                Section(body, "Presentación", introduction);
            }

            Categories(body, data.Categories);
            Regulations(body, data.Categories);

            if (data.Sanctions is { Length: > 0 } sanctions)
            {
                Section(body, "Sanciones", sanctions);
            }

            if (data.GeneralProvisions is { Length: > 0 } provisions)
            {
                Section(body, "Disposiciones generales", provisions);
            }

            if (data.ContactInfo is { Length: > 0 } contact)
            {
                Section(body, "Contacto", contact);
            }

            body.AppendChild(new SectionProperties(new PageMargin { Top = 1000, Bottom = 1000, Left = 1200, Right = 1200 }));
        }

        return stream.ToArray();
    }

    /// <summary>Points per EMU's own thousandths, at roughly the same size the PDF draws it.</summary>
    private const long LogoSizeEmu = 600000L;

    private static void Header(MainDocumentPart mainPart, Body body, BulletinData data)
    {
        var titleLines = new Paragraph[]
        {
            Paragraph(data.OrganizationName, size: 18, color: "757575"),
            Paragraph(data.CompetitionName, size: 36, bold: true),
            Paragraph("Convocatoria", size: 24, color: "1565C0"),
        };

        if (data.LogoBytes is not { } logo)
        {
            foreach (var line in titleLines)
            {
                body.AppendChild(line);
            }
        }
        else
        {
            // A borderless two-cell table rather than a floating picture: an
            // inline image sits on its own line, and the logo has to share
            // its line with the title instead of pushing it down the page.
            var table = new Table();

            table.AppendChild(new TableProperties(new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));

            var logoCell = new TableCell(new Paragraph(new Run(EmbeddedImage(mainPart, logo, LogoSizeEmu, LogoSizeEmu))));
            var titleCell = new TableCell(titleLines);

            table.AppendChild(new TableRow(logoCell, titleCell));
            body.AppendChild(table);
            body.AppendChild(Paragraph(string.Empty));
        }

        body.AppendChild(Paragraph(Subtitle(data), size: 18, spacingAfter: 200));
    }

    /// <summary>
    /// Embeds an image and returns the drawing that places it inline — the
    /// standard OOXML recipe for a picture in a run: an image part holding
    /// the bytes, and a DrawingML graphic in the run that points at it by
    /// relationship id.
    /// </summary>
    private static Drawing EmbeddedImage(MainDocumentPart mainPart, byte[] bytes, long widthEmu, long heightEmu)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);

        using (var stream = new MemoryStream(bytes))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = mainPart.GetIdOfPart(imagePart);

        return new Drawing(
            new WP.Inline(
                new WP.Extent { Cx = widthEmu, Cy = heightEmu },
                new WP.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new WP.DocProperties { Id = 1, Name = "Logo" },
                new WP.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0, Name = "logo.jpg" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0,
            });
    }

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

    /// <summary>A heading followed by the organizer's own paragraphs.</summary>
    private static void Section(Body body, string title, string text)
    {
        body.AppendChild(Heading(title));

        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            body.AppendChild(Paragraph(paragraph.Trim(), spacingAfter: 120));
        }
    }

    private static void Categories(Body body, IReadOnlyList<BulletinCategory> categories)
    {
        body.AppendChild(Heading("Categorías"));

        var table = new Table();

        table.AppendChild(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 6 },
            new BottomBorder { Val = BorderValues.Single, Size = 6 },
            new LeftBorder { Val = BorderValues.Single, Size = 6 },
            new RightBorder { Val = BorderValues.Single, Size = 6 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 })));

        table.AppendChild(Row(bold: true, "Categoría", "Género", "Edad", "Peso", "Cupo"));

        foreach (var category in categories)
        {
            table.AppendChild(Row(
                bold: false,
                category.Name,
                category.Gender ?? "Abierto",
                category.AgeRange ?? "Sin restricción",
                category.WeightRange ?? "Sin restricción",
                category.MaxRosterSize is { } max ? max.ToString() : "Sin límite"));
        }

        body.AppendChild(table);
        body.AppendChild(Paragraph(string.Empty));
    }

    private static void Regulations(Body body, IReadOnlyList<BulletinCategory> categories)
    {
        body.AppendChild(Heading("Reglamento y puntaje"));

        foreach (var category in categories)
        {
            body.AppendChild(Paragraph(category.Name, bold: true, spacingBefore: 160));
            body.AppendChild(Paragraph(category.PeriodsSummary));
            body.AppendChild(Paragraph($"Resultados: {string.Join(", ", category.Outcomes)}."));

            if (category.Tiebreakers.Count > 0)
            {
                body.AppendChild(Paragraph($"Desempate, en orden: {string.Join(" → ", category.Tiebreakers)}."));
            }

            if (category.IsJudged)
            {
                body.AppendChild(Paragraph(
                    "Corre además una etapa de clasificación previa, ordenada por puntaje de los "
                        + "jueces, antes de sortear la eliminatoria."));
            }
        }
    }

    private static Paragraph Heading(string text) =>
        Paragraph(text, size: 26, bold: true, color: "1565C0", spacingBefore: 240, spacingAfter: 120);

    private static Paragraph Paragraph(
        string text,
        int? size = null,
        bool bold = false,
        string? color = null,
        int? spacingBefore = null,
        int? spacingAfter = null)
    {
        var runProperties = new RunProperties();

        if (size is { } fontSize)
        {
            // Half-points, which is what OOXML measures a font in.
            runProperties.AppendChild(new FontSize { Val = fontSize.ToString() });
        }

        if (bold)
        {
            runProperties.AppendChild(new Bold());
        }

        if (color is { } fontColor)
        {
            runProperties.AppendChild(new Color { Val = fontColor });
        }

        var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var paragraphProperties = new ParagraphProperties();

        if (spacingBefore is not null || spacingAfter is not null)
        {
            paragraphProperties.AppendChild(new SpacingBetweenLines
            {
                Before = spacingBefore?.ToString(),
                After = spacingAfter?.ToString(),
            });
        }

        return new Paragraph(paragraphProperties, run);
    }

    private static TableRow Row(bool bold, params string[] cells) =>
        new(cells.Select(text => new TableCell(Paragraph(text, bold: bold))));
}
