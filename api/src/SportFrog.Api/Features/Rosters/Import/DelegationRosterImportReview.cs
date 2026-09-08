using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>One reviewed line of a delegation's squad sheet.</summary>
internal sealed record DelegationReviewedRow(
    int Number,
    string? Document,
    string Name,
    string? CategoryName,
    Guid? CategoryId,
    ImportOutcome Outcome,
    IReadOnlyList<string> Problems);

/// <summary>The whole delegation file, reviewed.</summary>
internal sealed record DelegationReviewedSheet(
    IReadOnlyList<DelegationReviewedRow> Rows,
    int Register,
    int Create,
    int AlreadyRegistered,
    int Rejected);

/// <summary>
/// Says what an uploaded delegation squad sheet would do, without doing any
/// of it.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterImportReview"/>: the eligibility rules are
/// still asked of the very same <see cref="RosterPolicy"/> that answers them
/// for a single enrollment, so a row this accepts is a row
/// <see cref="Teams.EnrollIndividual"/> would also accept. What is different
/// is what "already on the register" means: a squad's import checks one
/// team, because the file is scoped to one; a delegation's file is not
/// scoped to a category at all — each row names its own — so the question
/// this asks instead is whether the athlete already competes in the
/// category *that row* names, anywhere in this competition.
///
/// There is no shared roster cap to accumulate across rows the way the
/// squad importer accumulates one team's: every accepted row here creates
/// its own team of one, so the only cross-row questions are the same
/// athlete named twice in the file, and the same athlete already entered
/// through some earlier upload or through <see cref="Teams.EnrollIndividual"/>
/// directly.
/// </remarks>
internal sealed class DelegationRosterImportReview(SportFrogDbContext database, RosterPolicy policy)
{
    public async Task<DelegationReviewedSheet> ReviewAsync(
        Guid competitionId,
        Club club,
        IReadOnlyList<Category> categories,
        IReadOnlyList<DelegationSheetRow> rows,
        CancellationToken cancellationToken)
    {
        var categoryByName = categories
            .GroupBy(category => RosterSheet.Normalize(category.Name))
            .ToDictionary(group => group.Key, group => group.First());

        var documents = rows
            .Select(row => row.Document)
            .Where(document => document is not null)
            .Select(document => document!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var known = await database.Athletes
            .AsNoTracking()
            .Where(athlete => documents.Contains(athlete.DocumentId))
            .ToDictionaryAsync(
                athlete => athlete.DocumentId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Who already competes where, across the whole competition — not
        // only this club's own entries, since the question is "does this
        // person already have a place in this category", the same one
        // RosterPolicy asks of a single enrollment.
        var registeredPairs = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.Team!.Category!.CompetitionId == competitionId)
            .Select(entry => new { entry.Athlete!.DocumentId, entry.Team!.CategoryId })
            .ToListAsync(cancellationToken);

        var registered = new HashSet<(string Document, Guid CategoryId)>(
            registeredPairs.Select(pair => (pair.DocumentId.ToUpperInvariant(), pair.CategoryId)));

        var seenInFile = new HashSet<(string Document, Guid CategoryId)>();

        var reviewed = new List<DelegationReviewedRow>(rows.Count);

        foreach (var row in rows)
        {
            reviewed.Add(await ReviewRowAsync(
                club, categoryByName, row, known, registered, seenInFile, cancellationToken));
        }

        return new DelegationReviewedSheet(
            reviewed,
            reviewed.Count(row => row.Outcome == ImportOutcome.Register),
            reviewed.Count(row => row.Outcome == ImportOutcome.CreateAndRegister),
            reviewed.Count(row => row.Outcome == ImportOutcome.AlreadyRegistered),
            reviewed.Count(row => row.Outcome == ImportOutcome.Rejected));
    }

    private async Task<DelegationReviewedRow> ReviewRowAsync(
        Club club,
        Dictionary<string, Category> categoryByName,
        DelegationSheetRow row,
        Dictionary<string, Athlete> known,
        HashSet<(string Document, Guid CategoryId)> registered,
        HashSet<(string Document, Guid CategoryId)> seenInFile,
        CancellationToken cancellationToken)
    {
        var name = Name(row);
        var problems = new List<string>();

        Shape(row, problems);

        Category? category = null;

        if (row.CategoryName is { } categoryName
            && !categoryByName.TryGetValue(RosterSheet.Normalize(categoryName), out category))
        {
            problems.Add($"'{categoryName}' no es una categoría de esta competencia.");
        }

        // Nothing further is worth saying about a row that does not name a
        // real person in a real category: every remaining check would be
        // answered about something the row does not identify.
        if (problems.Count > 0 || row.Document is not { } document || category is null)
        {
            return new DelegationReviewedRow(
                row.Number, row.Document, name, row.CategoryName, category?.Id,
                ImportOutcome.Rejected, problems);
        }

        var pairKey = (document.ToUpperInvariant(), category.Id);

        if (!seenInFile.Add(pairKey))
        {
            // The single most common mistake in a squad sheet, and one that
            // nothing in the database would catch on its own: the same
            // person typed twice for the same category, usually with the
            // second row holding the correction.
            problems.Add($"{document} ya está en otra fila de este archivo para {category.Name}.");

            return new DelegationReviewedRow(
                row.Number, document, name, row.CategoryName, category.Id,
                ImportOutcome.Rejected, problems);
        }

        if (registered.Contains(pairKey))
        {
            // Already entered is not a problem and not work. Reported so the
            // operator can see the file was read, rather than wondering why
            // the count came up short — a delegation re-sending its file
            // after adding three names is how a spreadsheet is used.
            return new DelegationReviewedRow(
                row.Number, document, name, row.CategoryName, category.Id,
                ImportOutcome.AlreadyRegistered, problems);
        }

        var existing = known.GetValueOrDefault(document);

        // Never persisted: a team of one that would be created for this row.
        // RosterPolicy's checks either look across the whole category (sex,
        // birth date, whether this athlete already competes in it — already
        // ruled out above) or count what is registered against this exact
        // team, which for a team that does not exist yet is always zero —
        // exactly what it should find.
        var proposedTeam = new Team
        {
            Id = Guid.Empty,
            ClubId = club.Id,
            CategoryId = category.Id,
            Name = name,
            IsIndividual = true,
        };

        var violations = await policy.InspectAsync(
            proposedTeam,
            category,
            existing ?? Proposed(row, document),
            jerseyNumber: null,
            excluding: null,
            cancellationToken);

        problems.AddRange(violations.Select(violation => violation.Message));

        if (problems.Count > 0)
        {
            return new DelegationReviewedRow(
                row.Number, document, name, row.CategoryName, category.Id,
                ImportOutcome.Rejected, problems);
        }

        return new DelegationReviewedRow(
            row.Number,
            document,
            name,
            row.CategoryName,
            category.Id,
            existing is null ? ImportOutcome.CreateAndRegister : ImportOutcome.Register,
            problems);
    }

    /// <summary>
    /// What is wrong with the row as typed, before anybody asks whether the
    /// person may compete.
    /// </summary>
    private static void Shape(DelegationSheetRow row, List<string> problems)
    {
        if (row.Document is null)
        {
            problems.Add("El documento de identidad es obligatorio. Es lo que identifica a la persona.");
        }

        if (row.LastName is null)
        {
            problems.Add("Los apellidos son obligatorios.");
        }

        if (row.FirstName is null)
        {
            problems.Add("Los nombres son obligatorios.");
        }

        if (row.BirthDate is null)
        {
            problems.Add(row.BirthDateText is { } written
                ? $"'{written}' no es una fecha que se pueda leer. Tiene que estar escrita como "
                    + "2011-04-02 o 02/04/2011."
                : "La fecha de nacimiento es obligatoria.");
        }
        else if (row.BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            problems.Add($"{row.BirthDate:yyyy-MM-dd} no es una fecha pasada.");
        }

        if (row.Sex is not null && !Sex.IsAcceptable(row.Sex))
        {
            problems.Add($"'{row.Sex}' no es un sexo que el sistema registre. Tiene que ser "
                + $"{Sex.Female} o {Sex.Male}, o quedar vacío.");
        }

        if (row.CategoryName is null)
        {
            problems.Add("La categoría es obligatoria.");
        }
    }

    /// <summary>
    /// The person the row describes, as they would be created. Never saved
    /// and never tracked — see <see cref="RosterImportReview.Proposed"/>,
    /// which this mirrors exactly.
    /// </summary>
    private static Athlete Proposed(DelegationSheetRow row, string document) => new()
    {
        Id = Guid.Empty,
        FirstName = row.FirstName ?? string.Empty,
        LastName = row.LastName ?? string.Empty,
        DocumentId = document,
        BirthDate = row.BirthDate ?? default,
        Gender = Sex.Normalize(row.Sex),
        IsActive = true,
    };

    private static string Name(DelegationSheetRow row) =>
        string.Join(' ', new[] { row.LastName, row.FirstName }
            .Where(part => part is not null))
            is { Length: > 0 } written
            ? written
            : "(sin nombre)";
}
