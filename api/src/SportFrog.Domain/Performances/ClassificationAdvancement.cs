namespace SportFrog.Domain.Performances;

/// <summary>
/// Decides who advances from a finished classification stage into a
/// knockout, and in what order.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="Standings.GroupStageAdvancement"/> for a
/// stage that never had groups to keep apart in the first place. That one
/// ranks several pools and then has to reseed its result so
/// <see cref="Scheduling.Bracket.FirstRound"/>'s consecutive pairing does
/// not repeat a group-stage meeting; a classification stage ranks one pool,
/// so there is no such collision to avoid and nothing here to reseed —
/// strongest first, straight into the bracket, is the whole of it.
///
/// What the two do share is the one rule neither invents specially for
/// advancement: a tie at the cutoff is never split, the same way
/// <see cref="Statistics.Leaderboard"/> and <see cref="ClassificationRanking"/>
/// already refuse to split one everywhere else a ranking is shown.
/// </remarks>
public static class ClassificationAdvancement
{
    /// <param name="Seeded">
    /// Every qualifier, strongest first — handed to
    /// <see cref="Scheduling.Bracket.FirstRound"/> exactly as it stands,
    /// with no reordering to protect.
    /// </param>
    public sealed record Plan(IReadOnlyList<Guid> Seeded);

    /// <summary>
    /// Works out who advances, or explains in one sentence why these numbers
    /// do not fit this classification stage.
    /// </summary>
    public static (Plan? Plan, string? Problem) Build(
        IReadOnlyList<PerformanceEntry> entries, int qualifiers)
    {
        if (entries.Count == 0)
        {
            return (null, "Esta categoría no tiene una etapa de clasificación abierta.");
        }

        if (entries.Any(entry => entry.Status != PerformanceStatus.Scored))
        {
            return (null,
                "No todos los competidores tienen puntaje cargado, así que todavía no se sabe " +
                "quién clasifica.");
        }

        var qualifying = ClassificationRanking.Rank(entries)
            .Where(item => item.Position is { } position && position <= qualifiers)
            .Select(item => item.Entry.TeamId)
            .ToList();

        if (qualifying.Count < 2)
        {
            return (null,
                "Con este número clasifica menos de dos competidores; no hay eliminatoria que sortear.");
        }

        return (new Plan(qualifying), null);
    }
}
