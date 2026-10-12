using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Features.Documents;

/// <summary>The pictures one credential needs, fetched before anything is drawn.</summary>
/// <remarks>
/// Passed in rather than fetched here, the same reasoning <see cref="DocumentAssets"/>
/// is built on: object storage is slow, several of these are shared by every
/// credential of a batch, and this class has no business knowing where a
/// photograph is kept.
/// </remarks>
/// <param name="Icons">
/// A service's own picture, keyed by the <see cref="CredentialGrant.ItemId"/>
/// it belongs to rather than by its code — two items of different kinds are
/// free to share a code, so the code alone would not say which icon belongs
/// to which box. Absent for an item is the ordinary case: most are drawn as
/// their code in a box, and this exists for the handful — dining, today —
/// that are drawn as a picture instead.
/// </param>
/// <param name="Background">
/// The faint picture drawn once across the whole sheet, already faded by
/// <see cref="CredentialWatermark"/>. Absent leaves the sheet plain.
/// </param>
internal sealed record CredentialAssets(
    byte[]? Photo,
    byte[]? ClubLogo,
    byte[]? CompetitionLogo,
    byte[]? Qr,
    IReadOnlyDictionary<Guid, byte[]>? Icons = null,
    byte[]? Background = null);

/// <summary>
/// Draws the decreed credential: one 330 × 216 mm sheet, the back face on the
/// left and the front on the right, that folds down the middle into the
/// two-sided card.
/// </summary>
/// <remarks>
/// <see cref="DocumentRenderer"/>'s sibling, not its replacement. That one turns
/// a layout somebody designed into a page; this one turns the fixed structure in
/// <see cref="CredentialLayout"/> and a resolved set of facts —
/// <see cref="CredentialPrint"/> — into one, because nothing about this card is
/// still a layout decision left for a template to make.
///
/// Every block is placed by <see cref="Place"/>, a layer pinned to a corner by
/// padding, so a <see cref="CredentialBox"/> is a position rather than a
/// suggestion. Both faces are drawn by the same helpers, which is what keeps
/// the code row and the zone band identical on each side.
/// </remarks>
internal static class CredentialRenderer
{
    private const string PlaceholderName = "Nombre no disponible";

    /// <summary>Composes one credential as its own two-up PDF.</summary>
    public static byte[] Render(CredentialPrint print, CredentialAssets assets) =>
        BuildDocument(print, assets).GeneratePdf();

    /// <summary>
    /// Every credential of a batch as one PDF, each person's sheet its own
    /// page — what goes to a printer, the credential's counterpart to
    /// <see cref="PrintSheet"/>.
    /// </summary>
    /// <remarks>
    /// Concatenation, not imposition: a credential's own 330 × 216 mm sheet is
    /// already a full page, so there is no smaller unit to arrange several of
    /// across a bigger one.
    /// </remarks>
    public static byte[] RenderBatch(IReadOnlyList<(CredentialPrint Print, CredentialAssets Assets)> credentials) =>
        Document.Create(document =>
        {
            foreach (var (print, assets) in credentials)
            {
                document.Page(page => ComposePage(page, print, assets));
            }
        })
        .GeneratePdf();

    /// <summary>
    /// The document itself, kept separate from <see cref="Render"/>'s choice to
    /// turn it into PDF bytes, so calibration can rasterize it without a PDF reader.
    /// </summary>
    internal static IDocument BuildDocument(CredentialPrint print, CredentialAssets assets) =>
        Document.Create(document => document.Page(page => ComposePage(page, print, assets)));

    /// <summary>One sheet: size, margin, the watermark, and the two faces over it.</summary>
    private static void ComposePage(PageDescriptor page, CredentialPrint print, CredentialAssets assets)
    {
        page.Size((float)CredentialLayout.SheetWidthMm, (float)CredentialLayout.SheetHeightMm, Unit.Millimetre);
        page.Margin(0);
        page.PageColor(Colors.White);

        page.Content().Layers(layers =>
        {
            // The watermark sits in the primary layer so it lies behind both
            // faces, drawn once across the whole sheet rather than per face.
            var primary = layers.PrimaryLayer().Extend();

            if (assets.Background is { } background)
            {
                primary.AlignCenter().AlignMiddle().Image(background).FitArea();
            }

            Face(layers.Layer(), 0, container => DrawBack(container, print, assets));
            Face(layers.Layer(), CredentialLayout.FrontOffsetMm, container => DrawFront(container, print, assets));
        });
    }

    /// <summary>Pins one face — back or front — to its half of the sheet.</summary>
    private static void Face(IContainer layer, double leftMm, Action<IContainer> draw) =>
        draw(layer
            .AlignLeft()
            .AlignTop()
            .PaddingLeft((float)leftMm, Unit.Millimetre)
            .Width((float)CredentialLayout.FaceWidthMm, Unit.Millimetre)
            .Height((float)CredentialLayout.FaceHeightMm, Unit.Millimetre));

    /// <summary>Pins one block to its position within a face.</summary>
    private static void Place(IContainer layer, CredentialBox box, Action<IContainer> draw) =>
        draw(layer
            .AlignLeft()
            .AlignTop()
            .PaddingLeft((float)box.X, Unit.Millimetre)
            .PaddingTop((float)box.Y, Unit.Millimetre)
            .Width((float)box.Width, Unit.Millimetre)
            .Height((float)box.Height, Unit.Millimetre));

    // -------------------------------------------------------------------
    // Front face
    // -------------------------------------------------------------------

    private static void DrawFront(IContainer face, CredentialPrint print, CredentialAssets assets)
    {
        var accent = Accent(print.Context.AccentColorHex);

        face.Layers(layers =>
        {
            // Sizes the whole Layers component to the face; nothing is drawn into it.
            layers.PrimaryLayer().Extend();

            Place(layers.Layer(), CredentialLayout.FrontPhoto, container => Photo(container, assets.Photo));
            Place(layers.Layer(), CredentialLayout.FrontCompetitionLogo,
                container => Logo(container, assets.CompetitionLogo));
            Place(layers.Layer(), CredentialLayout.FrontCategoryBox, container => CategoryBox(container, print));

            Place(layers.Layer(), CredentialLayout.NameBlock, container => NameBlock(container, print, assets.ClubLogo));

            Place(layers.Layer(), CredentialLayout.FrontServicesRow,
                container => CodeRow(container, print.Rows[1], accent, assets.Icons));
            Place(layers.Layer(), CredentialLayout.FrontVisibleId,
                container => VisibleId(container, print.VisibleId, fontSize: 18));

            Place(layers.Layer(), CredentialLayout.CodeRow,
                container => CodeRow(container, print.Rows[0], accent, assets.Icons));

            Place(layers.Layer(), CredentialLayout.FooterBand, container => FooterBand(container, print));
        });
    }

    private static void Photo(IContainer container, byte[]? photo)
    {
        if (photo is null)
        {
            container.Background(Colors.Grey.Lighten3);
            return;
        }

        // Contained rather than stretched: a face squeezed to fill a box of the
        // wrong proportions is a distorted face, and the face is the whole point.
        container.Image(photo).FitArea();
    }

    /// <summary>
    /// The competition's mark, centred in its space. Fitted rather than
    /// stretched, and centred because a fitted picture otherwise hugs the top
    /// left and leaves its box looking like a block of its own.
    /// </summary>
    private static void Logo(IContainer container, byte[]? logo)
    {
        if (logo is not null)
        {
            container.AlignCenter().AlignMiddle().Image(logo).FitArea();
        }
    }

    private static void CategoryBox(IContainer container, CredentialPrint print)
    {
        var foreground = CredentialColor.ForegroundFor(print.CategoryColorHex);

        container.Background(print.CategoryColorHex).AlignCenter().AlignMiddle().Column(column =>
        {
            column.Item().AlignCenter().Text(print.CategoryCode).FontSize(34).Bold().FontColor(foreground);
            column.Item().AlignCenter().Text(print.CategoryName).FontSize(12).FontColor(foreground);
        });
    }

    private static void NameBlock(IContainer container, CredentialPrint print, byte[]? clubLogo) =>
        container.Column(column =>
        {
            column.Spacing(1);

            // Bold and large, the way the reference card prints it: the name is
            // the first thing read at a gate.
            column.Item().Text(
                string.IsNullOrWhiteSpace(print.Subject.FullName) ? PlaceholderName : print.Subject.FullName)
                .FontSize(22).Bold();

            column.Item().Text("Deportista").FontSize(12).FontColor(Colors.Grey.Darken2);

            column.Item().Row(row =>
            {
                if (clubLogo is not null)
                {
                    row.ConstantItem((float)CredentialLayout.ClubLogoSizeMm, Unit.Millimetre)
                        .Height((float)CredentialLayout.ClubLogoSizeMm, Unit.Millimetre)
                        .Image(clubLogo).FitArea();
                    row.ConstantItem(3);
                }

                row.RelativeItem().AlignMiddle().Text(print.Subject.ClubName).FontSize(12);
            });
        });

    /// <summary>
    /// The visible number, printed large and right-aligned beside the services
    /// it belongs with — the number a steward reads aloud against a list.
    /// </summary>
    private static void VisibleId(IContainer container, string text, float fontSize) =>
        container.AlignRight().AlignMiddle()
            .Text(text).FontSize(fontSize).Bold().FontFamily("Courier New", "Lato");

    // -------------------------------------------------------------------
    // Back face
    // -------------------------------------------------------------------

    private static void DrawBack(IContainer face, CredentialPrint print, CredentialAssets assets)
    {
        var accent = Accent(print.Context.AccentColorHex);

        face.Layers(layers =>
        {
            layers.PrimaryLayer().Extend();

            Place(layers.Layer(), CredentialLayout.LegalColumnLeft,
                container => LegalColumn(container, LeftHalf(print.Context.LegalText)));
            Place(layers.Layer(), CredentialLayout.LegalColumnRight,
                container => LegalColumn(container, RightHalf(print.Context.LegalText)));

            Place(layers.Layer(), CredentialLayout.BackMiniPhoto, container => Photo(container, assets.Photo));
            Place(layers.Layer(), CredentialLayout.BackIdentityText, container => IdentityText(container, print));

            Place(layers.Layer(), CredentialLayout.BackQr, container => Qr(container, assets.Qr));
            Place(layers.Layer(), CredentialLayout.BackVisibleId,
                container => VisibleId(container, print.VisibleId, fontSize: 12));

            Place(layers.Layer(), CredentialLayout.BackGlossary, container => Glossary(container, print, accent, assets.Icons));

            Place(layers.Layer(), CredentialLayout.CodeRow,
                container => CodeRow(container, print.Rows[0], accent, assets.Icons));

            Place(layers.Layer(), CredentialLayout.FooterBand, container => FooterBand(container, print));
        });
    }

    private static void LegalColumn(IContainer container, string text) =>
        container.Text(text).FontSize(7.5f).LineHeight(1.25f).FontColor(Colors.Grey.Darken3);

    /// <summary>
    /// A flowing split rather than a fixed character count: the legal notice is
    /// written by each organization and has no fixed length, so the two columns
    /// are only ever roughly even, the way a newspaper column break is.
    /// </summary>
    private static string LeftHalf(string legalText) => Halves(legalText).Left;

    private static string RightHalf(string legalText) => Halves(legalText).Right;

    private static (string Left, string Right) Halves(string legalText)
    {
        var trimmed = (legalText ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var middle = trimmed.Length / 2;
        var splitAt = trimmed.IndexOf(' ', middle);

        if (splitAt < 0)
        {
            splitAt = trimmed.Length;
        }

        return (trimmed[..splitAt].TrimEnd(), trimmed[splitAt..].TrimStart());
    }

    private static void IdentityText(IContainer container, CredentialPrint print) =>
        container.Column(column =>
        {
            column.Spacing(2);

            column.Item().Text(
                string.IsNullOrWhiteSpace(print.Subject.FullName) ? PlaceholderName : print.Subject.FullName)
                .FontSize(15);

            column.Item().Text("Deportista").FontSize(10).FontColor(Colors.Grey.Darken2);

            if (!string.IsNullOrWhiteSpace(print.Subject.DocumentId))
            {
                column.Item().Text(print.Subject.DocumentId).FontSize(12);
            }

            column.Item().Text(print.Subject.ClubName).FontSize(11);
        });

    private static void Qr(IContainer container, byte[]? qr)
    {
        if (qr is not null)
        {
            container.Image(qr).FitArea();
            return;
        }

        container.Background(Colors.Grey.Lighten3);
    }

    /// <summary>
    /// Every code the card prints, explained — in two columns, each entry a
    /// coloured code box and its name, so a steward can read any box on either
    /// face against this list.
    /// </summary>
    private static void Glossary(IContainer container, CredentialPrint print, Color accent, IReadOnlyDictionary<Guid, byte[]>? icons)
    {
        var half = (print.Grants.Count + 1) / 2;
        var left = print.Grants.Take(half).ToList();
        var right = print.Grants.Skip(half).ToList();

        container.Row(row =>
        {
            row.Spacing((float)CredentialLayout.LegalColumnGapMm, Unit.Millimetre);

            row.RelativeItem().Element(column => GlossaryColumn(column, left, accent, icons));
            row.RelativeItem().Element(column => GlossaryColumn(column, right, accent, icons));
        });
    }

    private static void GlossaryColumn(IContainer container, IReadOnlyList<CredentialGrant> grants, Color accent, IReadOnlyDictionary<Guid, byte[]>? icons) =>
        container.Column(column =>
        {
            column.Spacing((float)CredentialLayout.GlossaryEntryGapMm, Unit.Millimetre);

            foreach (var grant in grants)
            {
                column.Item().Height((float)CredentialLayout.GlossaryEntryHeightMm, Unit.Millimetre)
                    .Row(row =>
                    {
                        row.Spacing(3);

                        row.ConstantItem(18, Unit.Millimetre).Element(box => GrantBox(box, grant, icons, accent, fontSize: 10));
                        row.RelativeItem().AlignMiddle().Text(grant.Name).FontSize(9);
                    });
            }
        });

    // -------------------------------------------------------------------
    // Shared by both faces
    // -------------------------------------------------------------------

    /// <summary>
    /// The discipline and venue boxes, left-aligned at a fixed width each — how
    /// the reference card sets them, low on both faces.
    /// </summary>
    private static void CodeRow(
        IContainer container, IReadOnlyList<CredentialGrant> grants, Color accent,
        IReadOnlyDictionary<Guid, byte[]>? icons)
    {
        if (grants.Count == 0)
        {
            return;
        }

        container.Row(row =>
        {
            row.Spacing((float)CredentialLayout.BoxGapMm, Unit.Millimetre);

            foreach (var grant in grants)
            {
                row.ConstantItem((float)CredentialLayout.CodeBoxWidthMm, Unit.Millimetre)
                    .Element(box => GrantBox(box, grant, icons, accent, fontSize: 14));
            }
        });
    }

    /// <summary>
    /// The competition's own brand colour, or this system's default when it
    /// never set one — the same fallback every other document here reads its
    /// accent from.
    /// </summary>
    private static Color Accent(string? hex) =>
        string.IsNullOrWhiteSpace(hex) ? Colors.Blue.Darken2 : hex;

    /// <summary>
    /// One box of a grant: its picture when it has one, otherwise its code.
    /// A zone that carries a colour is filled with it, the way the reference
    /// prints its colour zones.
    /// </summary>
    private static void GrantBox(
        IContainer container, CredentialGrant grant, IReadOnlyDictionary<Guid, byte[]>? icons, Color accent, float fontSize)
    {
        if (grant.ColorHex is { } colour)
        {
            container.Background(colour).AlignCenter().AlignMiddle()
                .Text(grant.Code).FontSize(fontSize).Bold().FontColor(CredentialColor.ForegroundFor(colour));

            return;
        }

        var box = container.Background(Colors.Grey.Lighten3).Border(0.75f).BorderColor(accent)
            .AlignCenter().AlignMiddle().Padding(2);

        if (icons is not null && icons.TryGetValue(grant.ItemId, out var icon))
        {
            box.Image(icon).FitArea();
            return;
        }

        box.Text(grant.Code).FontSize(fontSize).Bold();
    }

    /// <summary>
    /// The band of access zones along the foot of the card — called once per
    /// face with the same <see cref="CredentialPrint"/>, which is what makes the
    /// two faces an exact replica of each other rather than two places that
    /// could drift apart. Each zone sits in its own equal share of the band.
    /// </summary>
    private static void FooterBand(IContainer container, CredentialPrint print)
    {
        var foreground = CredentialColor.ForegroundFor(print.BandColorHex);

        // The band is part of the decreed structure, so it is painted whether or
        // not this person holds a zone. Its size is stated here rather than left
        // to the row inside it: an empty row measures zero, and a background
        // paints only what its child occupies.
        container
            .Background(print.BandColorHex)
            .Width((float)CredentialLayout.FooterBand.Width, Unit.Millimetre)
            .Height((float)CredentialLayout.FooterBand.Height, Unit.Millimetre)
            .Row(row =>
            {
                // A zone with a colour is the band itself, not a letter printed on it:
                // the reference card shows its field of play as the blue strip, and
                // names it only in the glossary. Only the uncoloured zones print.
                foreach (var zone in print.Zones.Where(zone => zone.ColorHex is null))
                {
                    row.RelativeItem().AlignCenter().AlignMiddle()
                        .Text(zone.Code).FontSize(34).Bold().FontColor(foreground);
                }

                // A row of nothing measures nothing, and a background paints only
                // what its child occupies — which is how somebody holding no zone
                // at all ended up with a card that had no band.
                if (print.Zones.All(zone => zone.ColorHex is not null))
                {
                    row.RelativeItem();
                }
            });
    }
}
