namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Reads an optional filter out of a query string.
/// </summary>
/// <remarks>
/// A filter that was left blank and one that was not sent at all are the same
/// request: nothing was chosen, so nothing is narrowed. They do not arrive the
/// same way, though — a form submits every field it has, so an unselected
/// dropdown arrives as <c>?status=</c> and an untouched search box as
/// <c>?search=</c>, while a hand-built URL simply omits them.
///
/// Treating those differently is how a list quietly returns nothing: an empty
/// sport compared for equality matches no row, and the caller sees an empty
/// result where they asked for everything. The stricter variants are worse
/// still — refusing an empty state with a validation error tells someone their
/// form is broken when all they did was not use a filter.
/// </remarks>
public static class QueryFilter
{
    /// <summary>
    /// The value, or null when nothing was chosen.
    /// </summary>
    public static string? OrAbsent(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
