namespace SportFrog.Api.Features.Draw;

/// <summary>
/// Built from every <see cref="ICalendarDraw"/> the container knows about,
/// keyed by the format each one declares for itself.
/// </summary>
/// <remarks>
/// Constructed from <see cref="IEnumerable{T}"/> so the container's own
/// registrations are the only list of formats this ever reads — see
/// <c>SportFrog.Domain.Rules.MatchOutcomeRulesRegistry</c> for the same
/// choice made the same way.
/// </remarks>
internal sealed class CalendarDrawRegistry : ICalendarDrawRegistry
{
    private readonly IReadOnlyDictionary<string, ICalendarDraw> drawsByFormat;

    public CalendarDrawRegistry(IEnumerable<ICalendarDraw> draws)
    {
        // A duplicate format registered twice is a wiring mistake and should
        // fail at startup rather than have this silently keep whichever
        // happened to be registered last.
        drawsByFormat = draws.ToDictionary(candidate => candidate.Format, StringComparer.Ordinal);
    }

    public ICalendarDraw For(string format) =>
        drawsByFormat.TryGetValue(format, out var draw)
            ? draw
            : throw new InvalidOperationException(
                $"No hay un sorteo de calendario registrado para el formato '{format}'.");
}
