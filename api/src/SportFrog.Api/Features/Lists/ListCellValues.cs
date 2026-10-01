using System.Globalization;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// Turns one cell's raw value into the primitive its column's
/// <see cref="ListValueKind"/> promises.
/// </summary>
/// <remarks>
/// The one place <see cref="ListXlsx"/> and <see cref="ListPdf"/> both read a
/// <see cref="ListTable"/> cell's meaning from, so the two were never free to
/// disagree about what a cell says — a provider's bug (a goal count written
/// as a string, a date written as a number) surfaces once here, as a thrown
/// exception, rather than twice, as two renderers quietly showing two
/// different things.
/// </remarks>
internal static class ListCellValues
{
    public static double ToNumber(object value) => value switch
    {
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        _ => throw NotA("un número", value),
    };

    public static bool ToBoolean(object value) => value switch
    {
        bool b => b,
        _ => throw NotA("un booleano", value),
    };

    /// <summary>
    /// <paramref name="value"/> as a local <see cref="DateTime"/> — a
    /// <see cref="DateTimeOffset"/> is converted with
    /// <see cref="DateTimeOffset.ToLocalTime"/> first, the same conversion
    /// every other date shown in this system already applies before display.
    /// </summary>
    public static DateTime ToDateTime(object value) => value switch
    {
        DateTime dt => dt,
        DateTimeOffset dto => dto.ToLocalTime().DateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        _ => throw NotA("una fecha", value),
    };

    /// <summary>
    /// How a cell reads as plain text — what a PDF prints, and what a text
    /// column of an Excel sheet gets verbatim.
    /// </summary>
    public static string ToDisplayText(object? value, ListValueKind kind)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return kind switch
        {
            ListValueKind.Number => ToNumber(value).ToString("0.##", CultureInfo.InvariantCulture),
            ListValueKind.Boolean => ToBoolean(value) ? "Sí" : "No",
            ListValueKind.Date => ToDateTime(value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ListValueKind.DateTime => ToDateTime(value).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static ArgumentException NotA(string expected, object value) =>
        new($"El valor '{value}' (tipo {value.GetType().Name}) no se puede interpretar como {expected}.");
}
