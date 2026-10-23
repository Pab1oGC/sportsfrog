using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>One reviewed line of a staff sheet.</summary>
internal sealed record StaffReviewedRow(
    int Number,
    string? Document,
    string Name,
    string? Role,
    ImportOutcome Outcome,
    IReadOnlyList<string> Problems);

/// <summary>The whole staff file, reviewed.</summary>
internal sealed record StaffReviewedSheet(
    IReadOnlyList<StaffReviewedRow> Rows,
    int Register,
    int Create,
    int AlreadyRegistered,
    int Rejected);

/// <summary>
/// Says what an uploaded staff sheet would do, without doing any of it.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterImportReview"/>, asking the same eligibility
/// question of <see cref="StaffPolicy"/> that a single registration already
/// does — so a row this accepts is a row <see cref="RegisterStaff"/> would
/// also accept. The question itself is shorter here, because the policy is:
/// no age, no weight, no squad limit, only "not already on this bench" and
/// "at most one head coach at a time". The second of those is exactly the
/// one thing an import knows that a single registration does not: two rows
/// of the same file can both name a first head coach before either one has
/// reached the database, which is why <c>headCoachClaimedInFile</c> exists
/// below — the same role <c>seenJerseys</c> plays for a squad's dorsales.
/// </remarks>
internal sealed class StaffImportReview(SportFrogDbContext database, StaffPolicy policy)
{
    public async Task<StaffReviewedSheet> ReviewAsync(
        Team team,
        IReadOnlyList<StaffSheetRow> rows,
        CancellationToken cancellationToken)
    {
        var documents = rows
            .Select(row => row.Document)
            .Where(document => document is not null)
            .Select(document => document!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Everybody the file mentions who is already in the organization's
        // people, in one query rather than one per row — the same lookup
        // RosterImportReview makes for the same reason (RF-43).
        var known = await database.Athletes
            .AsNoTracking()
            .Where(athlete => documents.Contains(athlete.DocumentId))
            .ToDictionaryAsync(
                athlete => athlete.DocumentId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Who is actually on this bench today, so a file uploaded a second
        // time reports "already registered" instead of a collision — adding
        // a name and resending the whole file is how a spreadsheet is used.
        var onTeam = await database.TeamStaff
            .AsNoTracking()
            .Where(member => member.TeamId == team.Id && member.WithdrawnAt == null)
            .Select(member => member.Athlete!.DocumentId)
            .ToListAsync(cancellationToken);

        var activeDocuments = new HashSet<string>(onTeam, StringComparer.OrdinalIgnoreCase);
        var seenDocuments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var headCoachClaimedInFile = false;
        var reviewed = new List<StaffReviewedRow>(rows.Count);

        foreach (var row in rows)
        {
            var (review, claimsHeadCoach) = await ReviewRowAsync(
                team, row, known, activeDocuments, seenDocuments, headCoachClaimedInFile, cancellationToken);

            reviewed.Add(review);
            headCoachClaimedInFile |= claimsHeadCoach;
        }

        return new StaffReviewedSheet(
            reviewed,
            reviewed.Count(row => row.Outcome == ImportOutcome.Register),
            reviewed.Count(row => row.Outcome == ImportOutcome.CreateAndRegister),
            reviewed.Count(row => row.Outcome == ImportOutcome.AlreadyRegistered),
            reviewed.Count(row => row.Outcome == ImportOutcome.Rejected));
    }

    private async Task<(StaffReviewedRow Row, bool ClaimsHeadCoach)> ReviewRowAsync(
        Team team,
        StaffSheetRow row,
        Dictionary<string, Athlete> known,
        HashSet<string> activeDocuments,
        Dictionary<string, int> seenDocuments,
        bool headCoachClaimedInFile,
        CancellationToken cancellationToken)
    {
        var name = Name(row);
        var problems = new List<string>();

        Shape(row, problems);

        // Nothing further is worth saying about a row that does not
        // identify a real person with a real cargo: every remaining check
        // would be answered about something the row does not name.
        if (problems.Count > 0 || row.Document is not { } document || row.Role is not { } role)
        {
            return (Reject(row, name, problems), false);
        }

        if (seenDocuments.TryGetValue(document, out var earlier))
        {
            problems.Add($"El documento {document} ya está en la fila {earlier} de este archivo.");
        }
        else
        {
            seenDocuments[document] = row.Number;
        }

        if (activeDocuments.Contains(document))
        {
            // Already on the bench is not a problem and not work, unless the
            // row has something else wrong with it anyway — same reading
            // RosterImportReview gives somebody already on the squad.
            return problems.Count > 0
                ? (Reject(row, name, problems), false)
                : (new StaffReviewedRow(
                    row.Number, document, name, row.RoleText, ImportOutcome.AlreadyRegistered, problems), false);
        }

        if (role == StaffRole.HeadCoach && headCoachClaimedInFile)
        {
            problems.Add("Ya hay un director técnico para este equipo, asignado en otra fila de "
                + "este archivo. Un equipo tiene un solo director técnico a la vez.");
        }

        if (problems.Count > 0)
        {
            return (Reject(row, name, problems), false);
        }

        var existing = known.GetValueOrDefault(document);

        // The same question RegisterStaff asks through StaffPolicy for a
        // single registration — a brand-new person (no athleteId yet) can
        // never already be on a bench or already hold the head coach's
        // seat, which is exactly the truth Guid.Empty reads as here.
        var violations = await policy.InspectAsync(
            team.Id, existing?.Id ?? Guid.Empty, role, excluding: null, cancellationToken);

        problems.AddRange(violations.Select(violation => violation.Message));

        if (problems.Count > 0)
        {
            return (Reject(row, name, problems), false);
        }

        var outcome = existing is null ? ImportOutcome.CreateAndRegister : ImportOutcome.Register;

        return (
            new StaffReviewedRow(row.Number, document, name, row.RoleText, outcome, problems),
            role == StaffRole.HeadCoach);
    }

    private static StaffReviewedRow Reject(StaffSheetRow row, string name, List<string> problems) =>
        new(row.Number, row.Document, name, row.RoleText, ImportOutcome.Rejected, problems);

    /// <summary>
    /// What is wrong with the row as typed, before anybody asks whether the
    /// person may be on the bench.
    /// </summary>
    private static void Shape(StaffSheetRow row, List<string> problems)
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

        if (row.Role is null)
        {
            problems.Add(row.RoleText is { } written
                ? $"'{written}' no es un cargo reconocido. Tiene que ser uno de los que ofrece la plantilla."
                : "El cargo es obligatorio.");
        }
    }

    private static string Name(StaffSheetRow row) =>
        string.Join(' ', new[] { row.LastName, row.FirstName }
            .Where(part => part is not null))
            is { Length: > 0 } written
            ? written
            : "(sin nombre)";
}
