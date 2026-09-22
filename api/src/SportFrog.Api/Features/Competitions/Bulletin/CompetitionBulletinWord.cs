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
/// <see cref="BulletinData"/> and mirror the same look — accent colour,
/// heading sizes, table rules, the separator under the header — so a PDF and
/// a Word copy of the same bulletin never read as two different documents.
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

            // A doc-wide default run size AND font family — matches the PDF's
            // own page.DefaultTextStyle(10).FontFamily("Lato"): every
            // Paragraph() below that doesn't pass its own `size` or `color`
            // inherits this instead of whatever the opening application's
            // built-in "Normal" style happens to be (Calibri 11 in current
            // Word, something else in an older one or in another program
            // entirely). Without this, plain body text — the category table,
            // the organizer's prose, the rules per category — renders one
            // size larger and in a different typeface than the PDF, for
            // reasons that have nothing to do with either document's design.
            // Lato specifically because that is what the PDF now asks for too
            // (see CompetitionBulletinPdf.Render's own remark on why).
            //
            // The paragraph default also zeroes spacing before/after: Word's
            // own built-in "Normal" style — the one every paragraph here
            // falls back to, since none of them names a style — carries its
            // own spacing-after (commonly ~8pt) that QuestPDF's Column has no
            // equivalent of. Left alone, that default stacks on top of every
            // one of Header()'s title lines and the subtitle, spreading the
            // header out far more than the PDF's tightly-packed Column ever
            // is. Paragraphs that DO want space around them — Heading(),
            // Section()'s body text, HorizontalRule() — already pass their
            // own SpacingBetweenLines, which wins over this default.
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new DocDefaults(
                    new RunPropertiesDefault(
                        new RunPropertiesBaseStyle(
                            new RunFonts { Ascii = "Lato", HighAnsi = "Lato", ComplexScript = "Lato" },
                            new FontSize { Val = "20" })),
                    new ParagraphPropertiesDefault(
                        new ParagraphPropertiesBaseStyle(
                            new SpacingBetweenLines { Before = "0", After = "0" }))));
            stylesPart.Styles.Save();

            Header(mainPart, body, data);

            if (data.Introduction is { Length: > 0 } introduction)
            {
                Section(body, "Presentación", introduction, data.AccentColor);
            }

            Categories(body, data.Categories, data.AccentColor);
            Regulations(body, data.Categories, data.AccentColor);

            if (data.Sanctions is { Length: > 0 } sanctions)
            {
                Section(body, "Sanciones", sanctions, data.AccentColor);
            }

            if (data.GeneralProvisions is { Length: > 0 } provisions)
            {
                Section(body, "Disposiciones generales", provisions, data.AccentColor);
            }

            if (data.ContactInfo is { Length: > 0 } contact)
            {
                Section(body, "Contacto", contact, data.AccentColor);
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
            Paragraph("Convocatoria", size: 24, color: Accent(data.AccentColor)),
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

        body.AppendChild(Paragraph(Subtitle(data)));
        body.AppendChild(HorizontalRule());
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

    /// <summary>
    /// The rule under the header, closing off the title block the same way
    /// <c>CompetitionBulletinPdf.Header</c>'s <c>LineHorizontal</c> does — an
    /// otherwise-empty paragraph whose only content is its own bottom border.
    /// </summary>
    private static Paragraph HorizontalRule() => new(
        new ParagraphProperties(
            // CT_PPr orders pBdr before spacing — the other way round is a
            // schema violation, tolerated by Word but not by every reader.
            new ParagraphBorders(new BottomBorder
            {
                Val = BorderValues.Single, Size = 8, Space = 1, Color = GreyLighten1,
            }),
            new SpacingBetweenLines { Before = "120", After = "200" }));

    /// <summary>A heading followed by the organizer's own paragraphs.</summary>
    private static void Section(Body body, string title, string text, string? accentColor)
    {
        body.AppendChild(Heading(title, accentColor));

        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            body.AppendChild(Paragraph(paragraph.Trim(), spacingAfter: 120));
        }
    }

    /// <summary>Who may enter each category — the table a delegate checks a roster against.</summary>
    private static void Categories(Body body, IReadOnlyList<BulletinCategory> categories, string? accentColor)
    {
        body.AppendChild(Heading("Categorías", accentColor));

        var table = new Table();

        // Full page width, and no grid: same restraint as CompetitionBulletinPdf's
        // Th/Td, which only ever draw a bottom rule (dark under the header,
        // faint under each row) and leave every other edge open. A table-wide
        // TableBorders(...) here would give Word the boxed, spreadsheet-like
        // grid the PDF deliberately doesn't have.
        table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

        // A <w:tblGrid> is mandatory by schema even though Word tolerates its
        // absence, and the column widths below reproduce the PDF table's own
        // ColumnsDefinition ratio (2 : 1 : 2 : 1.5 : 1) instead of Word's
        // even five-way split.
        table.AppendChild(new TableGrid(
            ColumnWidths.Select(width => new GridColumn { Width = width.ToString() })));

        table.AppendChild(new TableRow(
            HeaderCell("Categoría", 0), HeaderCell("Género", 1), HeaderCell("Edad", 2), HeaderCell("Peso", 3), HeaderCell("Cupo", 4)));

        foreach (var category in categories)
        {
            table.AppendChild(new TableRow(
                DataCell(category.Name, 0),
                DataCell(category.Gender ?? "Abierto", 1),
                DataCell(category.AgeRange ?? "Sin restricción", 2),
                DataCell(category.WeightRange ?? "Sin restricción", 3),
                DataCell(category.MaxRosterSize is { } max ? max.ToString() : "Sin límite", 4)));
        }

        body.AppendChild(table);
        body.AppendChild(Paragraph(string.Empty));
    }

    /// <summary>How each category plays and how a match is decided.</summary>
    private static void Regulations(Body body, IReadOnlyList<BulletinCategory> categories, string? accentColor)
    {
        body.AppendChild(Heading("Reglamento y puntaje", accentColor));

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

    private static Paragraph Heading(string text, string? accentColor) =>
        Paragraph(text, size: 26, bold: true, color: Accent(accentColor), spacingBefore: 240, spacingAfter: 120);

    /// <summary>
    /// The competition's own colour, or the blue this bulletin always used
    /// before a competition could set one — same fallback and same source
    /// colour as <c>CompetitionBulletinPdf.Accent</c>'s <c>Colors.Blue.Darken2</c>,
    /// just spelled the way OOXML wants a colour: no leading '#'.
    /// </summary>
    private static string Accent(string? hex) =>
        string.IsNullOrWhiteSpace(hex) ? DefaultAccentHex : hex.TrimStart('#');

    // The exact hex QuestPDF's Colors.Blue.Darken2 / Grey.Lighten1 / Grey.Darken1
    // / Grey.Lighten2 resolve to (confirmed by reading them off the library
    // itself, not guessed) — a hand-typed guess at a "close enough" hex is how
    // this file and CompetitionBulletinPdf drifted apart the first time: the
    // previous version of this constant was "1565C0", a plausible-looking blue
    // that simply isn't the one QuestPDF's default renders.
    private const string DefaultAccentHex = "1976D2";
    private const string GreyLighten1 = "BDBDBD";
    private const string GreyDarken1 = "757575";
    private const string GreyLighten2 = "E0E0E0";

    // The Categorías table's column widths, in fiftieths of a percent
    // (summing to 5000 = 100%) — CompetitionBulletinPdf.Categories's own
    // ColumnsDefinition ratio: 2 : 1 : 2 : 1.5 : 1.
    private static readonly int[] ColumnWidths = [1333, 667, 1333, 1000, 667];

    private static TableCell HeaderCell(string text, int column) => new(
        new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = ColumnWidths[column].ToString() },
            new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 8, Color = GreyDarken1 })),
        Paragraph(text, bold: true));

    private static TableCell DataCell(string text, int column) => new(
        new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = ColumnWidths[column].ToString() },
            new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = GreyLighten2 })),
        Paragraph(text));

    private static Paragraph Paragraph(
        string text,
        int? size = null,
        bool bold = false,
        string? color = null,
        int? spacingBefore = null,
        int? spacingAfter = null)
    {
        // CT_RPr's own child sequence is b, ..., color, ..., sz, ... — Word's
        // real parser tolerates any order, but a stricter OOXML reader (or
        // the schema validator) does not, so bold and color are appended
        // before size rather than after it.
        var runProperties = new RunProperties();

        if (bold)
        {
            runProperties.AppendChild(new Bold());
        }

        if (color is { } fontColor)
        {
            runProperties.AppendChild(new Color { Val = fontColor });
        }

        if (size is { } fontSize)
        {
            // Half-points, which is what OOXML measures a font in.
            runProperties.AppendChild(new FontSize { Val = fontSize.ToString() });
        }

        var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var paragraphProperties = new ParagraphProperties();

        if (spacingBefore is not null || spacingAfter is not null)
        {
            var spacing = new SpacingBetweenLines();

            // Not `Before = spacingBefore?.ToString()`: the implicit
            // string-to-StringValue conversion wraps even a null string in a
            // non-null StringValue, so that still serializes as `w:before=""`
            // — invalid, since ST_SignedTwipsMeasure has no empty case —
            // rather than the attribute being left out. Only touching the
            // property when there is a real value avoids the conversion
            // altogether for the other one.
            if (spacingBefore is { } before)
            {
                spacing.Before = before.ToString();
            }

            if (spacingAfter is { } after)
            {
                spacing.After = after.ToString();
            }

            paragraphProperties.AppendChild(spacing);
        }

        return new Paragraph(paragraphProperties, run);
    }
}
