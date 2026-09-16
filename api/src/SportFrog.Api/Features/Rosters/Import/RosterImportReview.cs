using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>What would happen to one line of the spreadsheet.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(SnakeCaseEnumConverter<ImportOutcome>))]
public enum ImportOutcome
{
    /// <summary>The person is on the register already and would be registered for the team.</summary>
    Register,

    /// <summary>Nobody has that document yet, so they would be added to the register too.</summary>
    CreateAndRegister,

    /// <summary>They already play for this team. Nothing would be done.</summary>
    AlreadyRegistered,

    /// <summary>Something is wrong with the row, and it names what.</summary>
    Rejected,
}

/// <summary>One reviewed line.</summary>
internal sealed record ReviewedRow(
    int Number,
    string? Document,
    string Name,
    ImportOutcome Outcome,
    IReadOnlyList<string> Problems);

/// <summary>The whole file, reviewed.</summary>
internal sealed record ReviewedSheet(
    IReadOnlyList<ReviewedRow> Rows,
    int Register,
    int Create,
    int AlreadyRegistered,
    int Rejected);

/// <summary>
/// Says what an uploaded squad sheet would do, without doing any of it.
/// </summary>
/// <remarks>
/// The eligibility rules are not restated here. They are asked of
/// <see cref="RosterPolicy"/>, the same object that answers them when
/// somebody registers one player through the interface — because a file that
/// is accepted row by row and then refused player by player, or the reverse,
/// is worse than having no preview at all. The one thing an import knows that
/// a single registration does not is that the other rows exist, and that is
/// exactly what is added: duplicates inside the file, and the squad limit
/// counted against the rows already accepted above.
///
/// It costs a few queries per row, which is deliberate. A squad sheet is
/// twenty or thirty people; the alternative is a second copy of the
/// eligibility rules written to run in memory, and the day the two disagree
/// the file that the preview accepted is the file the import rejects.
/// </remarks>
internal sealed class RosterImportReview(SportFrogDbContext database, RosterPolicy policy)
{
    public async Task<ReviewedSheet> ReviewAsync(
        Team team,
        Category category,
        IReadOnlyList<SheetRow> rows,
        CancellationToken cancellationToken)
    {
        var documents = rows
            .Select(row => row.Document)
            .Where(document => document is not null)
            .Select(document => document!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Everybody the file mentions who is already on the register, in one
        // query rather than one per row. This is the lookup RF-43 rests on:
        // the document is what attaches a registration to the person who
        // already has a history here.
        var known = await database.Athletes
            .AsNoTracking()
            .Where(athlete => documents.Contains(athlete.DocumentId))
            .ToDictionaryAsync(
                athlete => athlete.DocumentId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Who is on this team now, so a file uploaded a second time reports
        // "already registered" instead of two dozen collisions. Adding three
        // names to a sheet and sending the whole thing again is how people
        // use a spreadsheet, and it has to be the boring case.
        var onTeam = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.TeamId == team.Id && entry.WithdrawnAt == null)
            .Select(entry => entry.Athlete!.DocumentId)
            .ToListAsync(cancellationToken);

        var registered = new HashSet<string>(onTeam, StringComparer.OrdinalIgnoreCase);

        var seenDocuments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenJerseys = new Dictionary<short, int>();

        var reviewed = new List<ReviewedRow>(rows.Count);
        var accepted = 0;

        foreach (var row in rows)
        {
            var review = await ReviewRowAsync(
                team, category, row, known, registered, seenDocuments, seenJerseys,
                accepted, cancellationToken);

            reviewed.Add(review);

            if (review.Outcome is ImportOutcome.Register or ImportOutcome.CreateAndRegister)
            {
                accepted++;
            }
        }

        return new ReviewedSheet(
            reviewed,
            reviewed.Count(row => row.Outcome == ImportOutcome.Register),
            reviewed.Count(row => row.Outcome == ImportOutcome.CreateAndRegister),
            reviewed.Count(row => row.Outcome == ImportOutcome.AlreadyRegistered),
            reviewed.Count(row => row.Outcome == ImportOutcome.Rejected));
    }

    private async Task<ReviewedRow> ReviewRowAsync(
        Team team,
        Category category,
        SheetRow row,
        Dictionary<string, Athlete> known,
        HashSet<string> registered,
        Dictionary<string, int> seenDocuments,
        Dictionary<short, int> seenJerseys,
        int accepted,
        CancellationToken cancellationToken)
    {
        var name = Name(row);
        var problems = new List<string>();

        Shape(row, problems);

        // Nothing further is worth saying about a row that has no document or
        // no name: every remaining check would be answered about a person the
        // row does not identify.
        if (problems.Count > 0 || row.Document is not { } document)
        {
            return new ReviewedRow(row.Number, row.Document, name, ImportOutcome.Rejected, problems);
        }

        if (seenDocuments.TryGetValue(document, out var earlier))
        {
            // The single most common mistake in a squad sheet, and one that
            // nothing in the database would catch on its own: the same person
            // typed twice, usually with the second row holding the correction.
            problems.Add($"El documento {document} ya está en la fila {earlier} de este archivo.");
        }
        else
        {
            seenDocuments[document] = row.Number;
        }

        if (row.Jersey is { } jersey)
        {
            if (seenJerseys.TryGetValue(jersey, out var wearer))
            {
                problems.Add(
                    $"El dorsal {jersey} ya está asignado en la fila {wearer} de este archivo.");
            }
            else
            {
                seenJerseys[jersey] = row.Number;
            }
        }

        if (registered.Contains(document))
        {
            // Already on the team is not a problem and not work. Reported so
            // the operator can see the file was read, rather than wondering
            // why the count came up short — adding three names to a sheet and
            // sending the whole thing again is how people use a spreadsheet.
            //
            // Unless the row has something wrong with it anyway, in which case
            // it is still wrong: a second row for somebody already registered
            // is a duplicate the operator wants to know about, not a quiet
            // nothing.
            return new ReviewedRow(
                row.Number,
                document,
                name,
                problems.Count > 0 ? ImportOutcome.Rejected : ImportOutcome.AlreadyRegistered,
                problems);
        }

        var existing = known.GetValueOrDefault(document);

        var violations = await policy.InspectAsync(
            team,
            category,
            existing ?? Proposed(row, document),
            row.Jersey,
            excluding: null,
            cancellationToken,
            pending: accepted);

        problems.AddRange(violations.Select(violation => violation.Message));

        if (problems.Count > 0)
        {
            return new ReviewedRow(row.Number, document, name, ImportOutcome.Rejected, problems);
        }

        return new ReviewedRow(
            row.Number,
            document,
            name,
            existing is null ? ImportOutcome.CreateAndRegister : ImportOutcome.Register,
            problems);
    }

    /// <summary>
    /// What is wrong with the row as typed, before anybody asks whether the
    /// person may play.
    /// </summary>
    private static void Shape(SheetRow row, List<string> problems)
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
                : "La fecha de nacimiento es obligatoria. La categoría admite un rango de fechas.");
        }
        else if (row.BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            problems.Add($"{row.BirthDate:yyyy-MM-dd} no es una fecha pasada.");
        }

        if (row.Sex is not null && !Sex.IsAcceptable(row.Sex))
        {
            // Worded here rather than taken from the shared requirement,
            // which the athlete endpoints also show and which is not part of
            // what was asked to be translated.
            problems.Add($"'{row.Sex}' no es un sexo que el sistema registre. Tiene que ser "
                + $"{Sex.Female} o {Sex.Male}, o quedar vacío.");
        }

        // Mirrors ck_athletes_weight_positive: mismo chequeo que
        // CreateAthlete/UpdateAthlete le hacen a un peso cargado a mano.
        if (row.WeightText is not null && row.Weight is null)
        {
            problems.Add($"'{row.WeightText}' no es un peso.");
        }
        else if (row.Weight is <= 0)
        {
            problems.Add("El peso tiene que ser mayor que cero.");
        }

        if (row.JerseyText is not null && row.Jersey is null)
        {
            problems.Add($"'{row.JerseyText}' no es un número de dorsal.");
        }
        else if (row.Jersey is { } number and (< 0 or > 999))
        {
            problems.Add($"El dorsal va entre 0 y 999, y acá dice {number}.");
        }
    }

    /// <summary>
    /// The person the row describes, as they would be created.
    /// </summary>
    /// <remarks>
    /// Never saved and never tracked. It exists so the eligibility rules can
    /// be asked about somebody who is not on the register yet, using the same
    /// code that asks about somebody who is — the alternative being a second
    /// set of age and sex checks written for people who happen to be new.
    ///
    /// The identifier stays empty on purpose: the checks that look for
    /// existing registrations then find none, which is the truth about
    /// somebody nobody has ever registered.
    /// </remarks>
    private static Athlete Proposed(SheetRow row, string document) => new()
    {
        Id = Guid.Empty,
        FirstName = row.FirstName ?? string.Empty,
        LastName = row.LastName ?? string.Empty,
        DocumentId = document,
        BirthDate = row.BirthDate ?? default,
        Gender = Sex.Normalize(row.Sex),
        WeightKg = row.Weight,
        IsActive = true,
    };

    private static string Name(SheetRow row) =>
        string.Join(' ', new[] { row.LastName, row.FirstName }
            .Where(part => part is not null))
            is { Length: > 0 } written
            ? written
            : "(sin nombre)";
}
