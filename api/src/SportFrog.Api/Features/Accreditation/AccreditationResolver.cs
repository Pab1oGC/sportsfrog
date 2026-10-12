using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Accreditation;

/// <summary>Why one resolved item is on the card.</summary>
public enum AccreditationItemSource
{
    /// <summary>Granted because the person's category carries it.</summary>
    Category,

    /// <summary>Granted or taken away by an exception recorded against the person.</summary>
    Override,
}

/// <summary>One entry of what a person ends up holding, ready to read or print.</summary>
public sealed record ResolvedAccreditationItem(
    Guid ItemId,
    AccreditationItemKind Kind,
    string Code,
    string Name,
    string? ColorHex,
    string? IconKey,
    AccreditationItemSource Source);

/// <summary>
/// What one person's accreditation actually grants, once their category's
/// package and their own exceptions are put together.
/// </summary>
/// <remarks>
/// The one place this computation happens. A category's package and a
/// person's overrides are two tables precisely so that four hundred athletes
/// can be accredited by choosing one category each rather than granted a zone
/// at a time — but nothing reads as "what does this card print" until the two
/// are merged, and merging them is the entire reason this class exists rather
/// than the two tables being read separately wherever the answer is needed.
///
/// Built for one person at a time because that is what reading a detail
/// screen or drawing one credential asks for. A batch of four hundred —
/// <see cref="SportFrog.Api.Features.Documents.IssueDocumentsJob"/>, once it
/// starts drawing this credential — resolves them the same way the roster
/// already reads them: most of a batch in one query, not one call per
/// subject.
/// </remarks>
public sealed class AccreditationResolver(SportFrogDbContext database)
{
    /// <summary>
    /// Everything this accreditation grants, in printing order, with the live
    /// details of each item.
    /// </summary>
    /// <remarks>
    /// Built on <see cref="ResolveManyAsync"/> with a single id — the merge
    /// rule lives in exactly one place. This reads the granted items' current
    /// code, name and colour, which is correct for a screen showing what an
    /// accreditation presently resolves to; drawing an already-issued
    /// credential again wants what it looked like when it was printed
    /// instead, which is why the job that prints a batch does not call this —
    /// see <see cref="ResolveManyAsync"/>'s own remarks.
    /// </remarks>
    public async Task<IReadOnlyList<ResolvedAccreditationItem>> ResolveAsync(
        Guid athleteAccreditationId, CancellationToken cancellationToken)
    {
        var resolved = (await ResolveManyAsync([athleteAccreditationId], cancellationToken))
            .GetValueOrDefault(athleteAccreditationId, EmptySources);

        if (resolved.Count == 0)
        {
            return [];
        }

        var items = await database.AccreditationItems
            .AsNoTracking()
            .Where(item => resolved.Keys.Contains(item.Id))
            .ToListAsync(cancellationToken);

        return [.. items
            .Select(item => new ResolvedAccreditationItem(
                item.Id, item.Kind, item.Code, item.Name, item.ColorHex, item.IconKey, resolved[item.Id]))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Code)];
    }

    private static readonly IReadOnlyDictionary<Guid, AccreditationItemSource> EmptySources =
        new Dictionary<Guid, AccreditationItemSource>();

    /// <summary>
    /// Which items each of several accreditations grants, and why — a
    /// category's package and a person's own overrides merged, the same rule
    /// <see cref="ResolveAsync"/> applies to one, but for many in three
    /// queries total rather than three per person.
    /// </summary>
    /// <remarks>
    /// Deliberately stops at item ids and their source: it does not read
    /// <see cref="AccreditationItem"/> rows at all, because its one caller —
    /// a credential batch — draws every item's code, name and colour from the
    /// batch's own frozen snapshot, not from the catalogue as it stands today.
    /// Reading four hundred items' live details here would be work thrown
    /// away the moment the caller reaches for the snapshot instead.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, AccreditationItemSource>>> ResolveManyAsync(
        IReadOnlyCollection<Guid> athleteAccreditationIds, CancellationToken cancellationToken)
    {
        if (athleteAccreditationIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyDictionary<Guid, AccreditationItemSource>>();
        }

        var categoryByAccreditation = await database.AthleteAccreditations
            .AsNoTracking()
            .Where(accreditation => athleteAccreditationIds.Contains(accreditation.Id))
            .Select(accreditation => new { accreditation.Id, accreditation.CategoryId })
            .ToDictionaryAsync(accreditation => accreditation.Id, accreditation => accreditation.CategoryId, cancellationToken);

        var categoryIds = categoryByAccreditation.Values.Distinct().ToList();

        var packageByCategory = (await database.AccreditationCategoryItems
                .AsNoTracking()
                .Where(link => categoryIds.Contains(link.CategoryId))
                .Select(link => new { link.CategoryId, link.ItemId })
                .ToListAsync(cancellationToken))
            .GroupBy(link => link.CategoryId)
            .ToDictionary(group => group.Key, group => group.Select(link => link.ItemId).ToHashSet());

        var overridesByAccreditation = (await database.AthleteAccreditationItems
                .AsNoTracking()
                .Where(exception => athleteAccreditationIds.Contains(exception.AccreditationId))
                .Select(exception => new { exception.AccreditationId, exception.ItemId, exception.Granted })
                .ToListAsync(cancellationToken))
            .GroupBy(exception => exception.AccreditationId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(exception => exception.ItemId, exception => exception.Granted));

        var result = new Dictionary<Guid, IReadOnlyDictionary<Guid, AccreditationItemSource>>();

        foreach (var accreditationId in athleteAccreditationIds)
        {
            if (!categoryByAccreditation.TryGetValue(accreditationId, out var categoryId))
            {
                continue;
            }

            var package = packageByCategory.GetValueOrDefault(categoryId, []);
            var overrides = overridesByAccreditation.GetValueOrDefault(accreditationId, []);

            var sources = new Dictionary<Guid, AccreditationItemSource>();

            foreach (var itemId in package)
            {
                if (!overrides.TryGetValue(itemId, out var granted) || granted)
                {
                    sources[itemId] = AccreditationItemSource.Category;
                }
            }

            foreach (var (itemId, granted) in overrides.Where(pair => pair.Value))
            {
                sources[itemId] = AccreditationItemSource.Override;
            }

            result[accreditationId] = sources;
        }

        return result;
    }
}
