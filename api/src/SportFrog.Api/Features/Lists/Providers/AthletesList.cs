using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Lists.Providers;

/// <summary>Every person registered by the organization, searchable by name or document and narrowable by gender.</summary>
/// <remarks>
/// Mirrors <c>Athletes.ReadAthletes.ListAsync</c>'s own search (name or
/// document, partial) and gender filter rather than sharing code with it:
/// that handler also resolves a photo link and paginates for a screen,
/// neither of which an export needs — a page of fifty is a screen's own
/// concern, not a list's. The age and weight ranges that handler also offers
/// are left out here: without a <see cref="ListParameterKind"/> a numeric
/// range fits today, adding them would mean inventing one for a filter
/// nobody has asked this list for yet.
///
/// Scoped to the whole organization rather than one category — the one
/// provider in this feature for which that is true — so this is also the one
/// most likely to meet <c>ReadLists</c>'s own row cap on a large roster; the
/// search filter exists as much to narrow that as to find someone by name.
/// </remarks>
internal sealed class AthletesList : IListProvider
{
    private static readonly IReadOnlyList<ListColumn> Columns =
    [
        new ListColumn("Documento", ListValueKind.Text),
        new ListColumn("Apellido y nombre", ListValueKind.Text),
        new ListColumn("Nacimiento", ListValueKind.Date),
        new ListColumn("Género", ListValueKind.Text),
        new ListColumn("Peso (kg)", ListValueKind.Number),
        new ListColumn("Activo", ListValueKind.Boolean),
    ];

    public string Slug => "deportistas";

    public string Label => "Deportistas";

    public IReadOnlyList<ListParameter> Parameters { get; } =
        [
            new ListParameter("search", ListParameterKind.Search, Required: false),
            new ListParameter("gender", ListParameterKind.Gender, Required: false),
        ];

    // Scoped to the whole organization, not to any one competition's sport —
    // see the class remarks.
    public Task<bool> AppliesToAsync(
        string sportCode, ScoreMode scoreMode, SportFrogDbContext database, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<ListTable?> LoadAsync(
        ListScope scope, SportFrogDbContext database, CancellationToken cancellationToken) =>
        LoadAsync(scope, scopedCompetitionIds: null, database, cancellationToken);

    /// <summary>
    /// The same list, narrowed to athletes enrolled in one of
    /// <paramref name="scopedCompetitionIds"/> when given.
    /// </summary>
    /// <remarks>
    /// This provider has no <see cref="ListParameterKind.Category"/> or
    /// <see cref="ListParameterKind.Team"/> parameter of its own — the one
    /// case <see cref="ListScopeGate"/> cannot gate on a single target — so
    /// <c>ReadLists</c> calls this overload directly for a scoped membership
    /// instead, passing <see cref="OrganizationContext.ScopedCompetitionIds"/>
    /// rather than refusing the whole list. Null means organization-wide, the
    /// same as calling the interface method; an empty set (a membership
    /// scoped to clubs only, never to any competition) correctly narrows to
    /// nobody, not to everybody.
    /// </remarks>
    internal async Task<ListTable?> LoadAsync(
        ListScope scope,
        IReadOnlySet<Guid>? scopedCompetitionIds,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var search = QueryFilter.OrAbsent(scope.GetString("search"));
        var gender = QueryFilter.OrAbsent(scope.GetString("gender"));

        IReadOnlySet<Guid>? eligibleAthleteIds = scopedCompetitionIds is null
            ? null
            : (await (
                from entry in database.RosterEntries
                join team in database.Teams on entry.TeamId equals team.Id
                join category in database.Categories on team.CategoryId equals category.Id
                where scopedCompetitionIds.Contains(category.CompetitionId)
                select entry.AthleteId
            ).Distinct().ToListAsync(cancellationToken)).ToHashSet();

        var athletes = await database.Athletes
            .AsNoTracking()
            .Where(athlete => eligibleAthleteIds == null || eligibleAthleteIds.Contains(athlete.Id))
            .Where(athlete => search == null
                || EF.Functions.ILike(athlete.DocumentId, $"%{search}%")
                || EF.Functions.ILike(athlete.LastName, $"%{search}%")
                || EF.Functions.ILike(athlete.FirstName, $"%{search}%"))
            .Where(athlete => gender == null || athlete.Gender == gender)
            .OrderBy(athlete => athlete.LastName)
            .ThenBy(athlete => athlete.FirstName)
            .Select(athlete => new
            {
                athlete.DocumentId,
                athlete.FirstName,
                athlete.LastName,
                athlete.BirthDate,
                athlete.Gender,
                athlete.WeightKg,
                athlete.IsActive,
            })
            .ToListAsync(cancellationToken);

        var rows = athletes
            .Select(athlete => (IReadOnlyList<object?>)
            [
                athlete.DocumentId,
                $"{athlete.LastName}, {athlete.FirstName}",
                athlete.BirthDate,
                athlete.Gender ?? "-",
                athlete.WeightKg,
                athlete.IsActive,
            ])
            .ToList();

        return new ListTable("Deportistas", null, Columns, [new ListSection(null, rows)]);
    }
}
