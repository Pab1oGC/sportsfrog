namespace SportFrog.Api.Features.Lists;

/// <summary>
/// Copy shared by every list renderer. One place for it so an empty export
/// says the same thing whether it came out as Excel or as PDF — the two are
/// read side by side often enough that disagreeing on this would be noticed.
/// </summary>
internal static class ListMessages
{
    public const string NoData = "No hay datos para este alcance.";
}
