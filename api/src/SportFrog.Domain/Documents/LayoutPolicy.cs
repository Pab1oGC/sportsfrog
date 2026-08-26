
namespace SportFrog.Domain.Documents;

/// <summary>Something a layout says that cannot be printed.</summary>
public sealed record LayoutFault(string Property, string Message);

/// <summary>
/// Whether a design can actually be produced.
/// </summary>
/// <remarks>
/// This is where the schema's promise that the column "does not accept
/// executable content" is kept. The guarantee is not a filter looking for
/// dangerous strings — those are always a step behind — it is that a layout
/// is a closed shape: a known set of sources, a known set of typefaces, a
/// known set of alignments, and numbers between nought and one. Anything the
/// editor invents is refused here rather than stored and discovered later by
/// whatever reads the column next.
///
/// The other half is the storage key. A background is an object, and a layout
/// naming an object of another organization would print that organization's
/// artwork — or, worse, read a file it was never meant to. Every key is
/// checked against the prefix of the organization saving the design.
/// </remarks>
public static class LayoutPolicy
{
    /// <summary>
    /// Past any credential and short of a design nobody could have laid out
    /// by hand. A face carrying thousands of fields is a mistake or an
    /// attempt to make the generator work forever on one card.
    /// </summary>
    private const int MaximumFields = 60;

    /// <summary>
    /// Text taller than half the card is not a design decision, and the
    /// bound catches a decimal typed in the wrong units.
    /// </summary>
    private const double MaximumTextSize = 0.5;

    private const int MaximumLabelLength = 120;

    /// <summary>
    /// Nothing narrower than a hair's breadth of the card. A zero-sized box
    /// is a field somebody cannot see and cannot select to fix.
    /// </summary>
    private const double MinimumExtent = 0.005;

    /// <summary>
    /// Everything wrong with the design, or nothing.
    /// </summary>
    /// <remarks>
    /// All of it at once. Somebody laying out a credential should learn that
    /// the photograph falls off the edge and the surname uses a typeface that
    /// does not exist in one answer, not across two attempts.
    /// </remarks>
    /// <param name="ownsBackground">
    /// Whether a stored object belongs to the organization saving the design.
    ///
    /// Passed in rather than known here, and that is the point: it is the one
    /// thing a layout says that reaches outside itself. How a storage key is
    /// shaped is not a rule of the document — it is a fact about where files
    /// are kept — so the domain asks the question and lets the caller answer.
    /// </param>
    public static IReadOnlyList<LayoutFault> Inspect(
        TemplateLayout layout,
        DocumentKind kind,
        Func<string, bool> ownsBackground)
    {
        var faults = new List<LayoutFault>();

        Inspect(layout.Front, "front", kind, ownsBackground, faults);

        if (layout.Back is { } back)
        {
            Inspect(back, "back", kind, ownsBackground, faults);
        }

        return faults;
    }

    private static void Inspect(
        TemplateFace face,
        string side,
        DocumentKind kind,
        Func<string, bool> ownsBackground,
        List<LayoutFault> faults)
    {
        if (face.AspectRatio is <= 0.2 or > 5)
        {
            faults.Add(new LayoutFault(
                $"{side}.aspectRatio",
                "La proporción de la cara tiene que estar entre 0.2 y 5."));
        }

        if (face.BackgroundKey is { Length: > 0 } key
            && !ownsBackground(key))
        {
            // A design naming somebody else's object. Refused rather than
            // ignored: it is the one thing in a layout that reaches outside
            // the organization, so it is the one thing worth being loud about.
            faults.Add(new LayoutFault(
                $"{side}.backgroundKey",
                "Ese fondo no pertenece a esta organización."));
        }

        if (face.Fields.Count > MaximumFields)
        {
            faults.Add(new LayoutFault(
                $"{side}.fields",
                $"Una cara admite hasta {MaximumFields} campos."));
        }

        for (var index = 0; index < face.Fields.Count; index++)
        {
            Inspect(face.Fields[index], $"{side}.fields[{index}]", kind, faults);
        }
    }

    private static void Inspect(
        TemplateField field,
        string at,
        DocumentKind kind,
        List<LayoutFault> faults)
    {
        if (TemplateDesign.Source(field.Source) is not { } source)
        {
            faults.Add(new LayoutFault(
                $"{at}.source",
                $"'{field.Source}' no es algo que este sistema sepa imprimir."));

            // Nothing else is worth saying: every remaining rule depends on
            // what kind of thing this was supposed to be.
            return;
        }

        if (!source.Kinds.Contains(kind))
        {
            faults.Add(new LayoutFault(
                $"{at}.source",
                $"'{source.Label}' no se puede imprimir en este tipo de documento."));
        }

        if (field.X is < 0 or > 1 || field.Y is < 0 or > 1)
        {
            faults.Add(new LayoutFault(
                at, "La posición se expresa entre 0 y 1, relativa a la cara."));
        }

        if (source.Shape == FieldShape.Image)
        {
            InspectBox(field, at, faults);
        }
        else
        {
            InspectText(field, at, faults);
        }

        if (field.Font is { } font && !TemplateDesign.HasFont(font))
        {
            faults.Add(new LayoutFault(
                $"{at}.font",
                $"'{font}' no es una de las tipografías disponibles."));
        }

        if (field.Align is { } align && !TemplateDesign.Alignments.Contains(align))
        {
            faults.Add(new LayoutFault(
                $"{at}.align",
                $"La alineación es una de: {string.Join(", ", TemplateDesign.Alignments)}."));
        }

        if (field.Fit is { } fit && !TemplateDesign.Fits.Contains(fit))
        {
            faults.Add(new LayoutFault(
                $"{at}.fit",
                $"El ajuste es uno de: {string.Join(", ", TemplateDesign.Fits)}."));
        }

        if (field.Color is { } color && !IsColor(color))
        {
            faults.Add(new LayoutFault(
                $"{at}.color", "El color se escribe como #rrggbb."));
        }
    }

    /// <summary>
    /// A picture: it has a box, and the box is on the card.
    /// </summary>
    private static void InspectBox(TemplateField field, string at, List<LayoutFault> faults)
    {
        if (field.W is not { } width || field.H is not { } height)
        {
            faults.Add(new LayoutFault(at, "Una imagen necesita ancho y alto."));
            return;
        }

        if (width < MinimumExtent || height < MinimumExtent)
        {
            faults.Add(new LayoutFault(at, "La imagen es demasiado pequeña para verse."));
            return;
        }

        // Off the edge is the mistake this catches, and it is the commonest
        // one in a drag-and-drop editor: something is nudged until it looks
        // right against a mock-up of one shape and hangs off a card of
        // another.
        if (field.X + width > 1.0001 || field.Y + height > 1.0001)
        {
            faults.Add(new LayoutFault(at, "La imagen se sale de la cara."));
        }
    }

    /// <summary>Words: they have a height, and shrinking has a floor.</summary>
    private static void InspectText(TemplateField field, string at, List<LayoutFault> faults)
    {
        if (field.Size is not { } size)
        {
            faults.Add(new LayoutFault($"{at}.size", "Un texto necesita un tamaño."));
        }
        else if (size is <= 0 or > MaximumTextSize)
        {
            faults.Add(new LayoutFault(
                $"{at}.size",
                $"El tamaño del texto se expresa entre 0 y {MaximumTextSize} de la altura de la cara."));
        }
        else if (field.MinSize is { } minimum && (minimum <= 0 || minimum > size))
        {
            faults.Add(new LayoutFault(
                $"{at}.minSize",
                "El tamaño mínimo tiene que ser mayor que cero y no mayor que el tamaño."));
        }

        if (field.Source == "text")
        {
            if (string.IsNullOrWhiteSpace(field.Text))
            {
                faults.Add(new LayoutFault($"{at}.text", "Un texto fijo necesita decir algo."));
            }
            else if (field.Text.Length > MaximumLabelLength)
            {
                faults.Add(new LayoutFault(
                    $"{at}.text",
                    $"Un texto fijo admite hasta {MaximumLabelLength} caracteres."));
            }
        }
        else if (field.Text is not null)
        {
            // A field that takes its words from a row and also carries words
            // of its own is two designs in one place, and only one of them
            // would be printed.
            faults.Add(new LayoutFault(
                $"{at}.text",
                "Solo un texto fijo lleva texto propio; los demás lo toman del dato."));
        }
    }

    private static bool IsColor(string value) =>
        value.Length == 7
        && value[0] == '#'
        && value[1..].All(Uri.IsHexDigit);
}
