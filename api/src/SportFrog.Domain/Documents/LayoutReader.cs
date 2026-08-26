using System.Text.Json;

namespace SportFrog.Domain.Documents;

/// <summary>
/// Turns the design a request carries into a layout, or says why it is not one.
/// </summary>
/// <remarks>
/// The layout arrives as raw JSON and is read here rather than being bound
/// straight to <see cref="TemplateLayout"/>, and the reason is the same one
/// that keeps every enum on this API a string in its contract: a request the
/// framework cannot deserialize never reaches a validator, so it comes back
/// as a reader failure with no usable body — a stack trace in development and
/// nothing at all in production.
///
/// That matters more here than anywhere else, because refusing unknown
/// properties is the whole point. A designer whose editor sends one property
/// this system does not know should be told which property, not handed an
/// empty four hundred.
/// </remarks>
public static class LayoutReader
{
    /// <summary>
    /// How a layout is written on the wire: camel case, like every other
    /// contract. The column stores it in snake case, which is a different
    /// question answered somewhere else.
    /// </summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static bool TryRead(
        JsonElement element,
        out TemplateLayout layout,
        out LayoutFault fault)
    {
        layout = null!;
        fault = null!;

        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            fault = new LayoutFault("layout", "El diseño es obligatorio.");
            return false;
        }

        try
        {
            if (element.Deserialize<TemplateLayout>(Wire) is not { } read)
            {
                fault = new LayoutFault("layout", "El diseño es obligatorio.");
                return false;
            }

            if (read.Front is null)
            {
                fault = new LayoutFault("layout.front", "El diseño necesita una cara frontal.");
                return false;
            }

            layout = read;
            return true;
        }
        catch (JsonException failure)
        {
            // The path is the useful half. "$.front.fields[3].onRender" tells
            // whoever wrote the editor exactly where to look; the library's
            // own English sentence about .NET members does not.
            fault = new LayoutFault(
                Where(failure.Path),
                "El diseño trae algo que este sistema no reconoce. Solo se admiten las "
                    + "propiedades documentadas del diseño.");

            return false;
        }
    }

    /// <summary>The offending place, in the shape the other faults use.</summary>
    private static string Where(string? path) =>
        string.IsNullOrEmpty(path) || path == "$"
            ? "layout"
            : "layout" + path[1..];
}
