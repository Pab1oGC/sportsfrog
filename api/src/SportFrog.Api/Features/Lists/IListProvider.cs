using System.Text.Json.Serialization;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// What kind of thing one named input selects — the hint a catalog or a
/// filter form needs to pick the right control for it, without knowing which
/// list declared it.
/// </summary>
/// <remarks>
/// Public, unlike the rest of this feature's contracts: it travels in
/// <see cref="ReadLists.CatalogEntry"/>, the one part of <c>Features.Lists</c>
/// a caller outside this assembly actually reads.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumConverter<ListParameterKind>))]
public enum ListParameterKind
{
    Competition,
    Category,
    Team,
    Metric,
    Position,
    Search,
    Gender,
    DateRange,
}

/// <summary>
/// One named input a list declares it needs, before it can run. Public for
/// the same reason <see cref="ListParameterKind"/> is.
/// </summary>
public sealed record ListParameter(string Name, ListParameterKind Kind, bool Required);

/// <summary>
/// The named values one list run was asked for — a category, a team, a
/// metric — before any provider has decided what they mean.
/// </summary>
/// <remarks>
/// Kept as raw strings rather than typed properties for the same reason
/// <see cref="IListProvider.Parameters"/> is: this contract does not know
/// the parameters any given provider declares, so it cannot know their
/// shapes either. A provider parses its own values out of this scope the
/// way a minimal API handler parses its own route and query values — the
/// difference is only that no framework is doing the binding on a provider's
/// behalf, since the one endpoint that builds a <see cref="ListScope"/> is
/// shared by every slug and cannot know any one provider's parameter types.
/// </remarks>
internal sealed class ListScope
{
    private readonly IReadOnlyDictionary<string, string> values;

    public ListScope(IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
    }

    public static ListScope Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The raw value named <paramref name="name"/>, or null when it was not given.</summary>
    public string? GetString(string name) => values.TryGetValue(name, out var value) ? value : null;

    /// <summary>The value named <paramref name="name"/> as a <see cref="Guid"/>, or null when absent or not one.</summary>
    public Guid? GetGuid(string name) =>
        GetString(name) is { } raw && Guid.TryParse(raw, out var parsed) ? parsed : null;

    /// <summary>The value named <paramref name="name"/> as an <see cref="int"/>, or null when absent or not one.</summary>
    public int? GetInt(string name) =>
        GetString(name) is { } raw && int.TryParse(raw, out var parsed) ? parsed : null;
}

/// <summary>
/// One exportable list: a slug it is cataloged and requested under, the
/// inputs it needs named, and how to load it.
/// </summary>
/// <remarks>
/// The seam a new list is added through — the same shape as
/// <see cref="SportFrog.Api.Features.Draw.ICalendarDraw"/>, resolved through
/// <see cref="IListRegistry"/> rather than a switch over a slug, so a list
/// that does not exist yet is a class that does not exist yet, not a case
/// added to one that already does.
///
/// A provider that needs more than the database to answer — an
/// <c>ObjectStore</c> for a photo link, an <c>AthletePhoto</c> reader — takes
/// it through its own constructor, injected the way any other scoped service
/// is. This contract fixes only what every list shares.
/// </remarks>
internal interface IListProvider
{
    /// <summary>The key this list is resolved and requested by. Stable, since a saved link names it.</summary>
    string Slug { get; }

    /// <summary>What a person choosing this list from a catalog sees.</summary>
    string Label { get; }

    /// <summary>What this list needs named before it can run — a catalog builds a filter form from these.</summary>
    IReadOnlyList<ListParameter> Parameters { get; }

    /// <summary>
    /// Whether this list has anything to say for <paramref name="sportCode"/>.
    /// A catalog scoped to one competition filters by this, the same
    /// question <see cref="SportFrog.Api.Features.Lists.Providers.ClassificationList"/>
    /// and <see cref="SportFrog.Api.Features.Lists.Providers.StandingsList"/>
    /// already answer for themselves before they ever load a row — this just
    /// lets a caller ask it without loading anything.
    ///
    /// True for a list that does not depend on a sport at all: an
    /// organization-wide roster, a team's own squad, a champion resolved the
    /// same way (bracket, table, or classification) regardless of format.
    /// False for a list built on a shape a given <paramref name="scoreMode"/>
    /// genuinely does not have — a judged category runs no bracket and no
    /// table for <see cref="SportFrog.Api.Features.Lists.Providers.StandingsList"/>
    /// to read. Database-backed, not a pure function of
    /// <paramref name="scoreMode"/>, for the two providers whose shape
    /// depends on the sport's own metric catalog rather than its score mode
    /// alone: <see cref="SportFrog.Api.Features.Lists.Providers.LeadersList"/>
    /// has nothing to rank for a sport whose metrics never affect the score
    /// (volleyball's points are tallied by set, not summed), and
    /// <see cref="SportFrog.Api.Features.Lists.Providers.CardsList"/> has
    /// nothing to book for a sport with no card metric at all (basketball,
    /// volleyball) — both would otherwise show up in the catalog as an
    /// always-empty, visibly football-shaped list for a sport that has
    /// neither goals nor cards.
    /// </summary>
    Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken);

    /// <summary>
    /// The list's rows for this <paramref name="scope"/>, or null when
    /// nothing in it resolves to data — an unknown category, a team with no
    /// roster — so the caller can answer with "not found" rather than
    /// exporting an empty file as if that were the real answer.
    /// </summary>
    Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken);
}
