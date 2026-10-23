using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>
/// The shape of the sheet a club fills in to register a team's technical
/// staff and board.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterSheet"/>, not a variant of it: a coach or a
/// doctor has no shirt, no position and none of a squad's eligibility rules
/// (see <see cref="StaffPolicy"/>) — a cargo (role) is the one thing this
/// sheet needs that a player's does not. Kept as its own definition, on its
/// own data tab (<see cref="DataSheet"/>), precisely so the two files are
/// never one spreadsheet with two kinds of row mixed into it: a squad sheet
/// uploaded here is missing the one column ("Cargo") this sheet requires,
/// and is refused by that absence alone, the same way a staff sheet uploaded
/// against the roster endpoint is missing "Dorsal"'s sibling columns there.
///
/// <see cref="RosterSheet.Normalize"/> and
/// <see cref="SportFrog.Api.Features.Rosters.Import.WorkbookGuard"/> are
/// reused as-is: neither has anything specific to a squad, and sharing them
/// keeps the one zip-bomb defence and the one accent-and-case rule in a
/// single place instead of two that could drift.
/// </remarks>
internal static class StaffSheet
{
    public const string DataSheet = "CuerpoTecnico";

    /// <summary>Where the team's identity is stamped, out of the way.</summary>
    public const string MarkerSheet = "sportfrog";

    public const int Version = 1;

    public const int HeaderRow = 1;

    public const int FirstDataRow = 2;

    /// <summary>Past any bench a team fields and short of a pasted database.</summary>
    public const int MaximumRows = 200;

    public static readonly SheetColumn Document =
        new("Documento", "Documento de identidad. Es lo que identifica a la persona: si ya está registrada, se la reutiliza.", Required: true);

    public static readonly SheetColumn LastName =
        new("Apellidos", "Apellidos, como figuran en el documento.", Required: true);

    public static readonly SheetColumn FirstName =
        new("Nombres", "Nombres, como figuran en el documento.", Required: true);

    public static readonly SheetColumn BirthDate =
        new("Fecha de nacimiento", "Fecha, no texto.", Required: true);

    public static readonly SheetColumn Role =
        new("Cargo", "Qué hace para el equipo: una de las opciones de la lista.", Required: true);

    /// <summary>
    /// The columns, in the order the template lays them out. No dorsal, no
    /// posición, no apoderado: none of them mean anything for somebody who
    /// does not play, and <see cref="StaffPolicy"/> never asks about age,
    /// sex or weight the way a roster entry's does — the whole reason this
    /// sheet is five columns and a squad's is ten.
    /// </summary>
    public static readonly IReadOnlyList<SheetColumn> Columns = [Document, LastName, FirstName, BirthDate, Role];

    /// <summary>Rows of the hidden sheet that says what this workbook is.</summary>
    public const int MarkerNote = 1;
    public const int MarkerVersion = 2;
    public const int MarkerOrganization = 3;
    public const int MarkerTeam = 4;
    public const int MarkerSubject = 5;

    /// <summary>
    /// A role's label on the sheet, in the same words StaffPanel's own
    /// dropdown already shows — see the frontend's <c>ROLES</c> array, which
    /// this mirrors by hand because the enum crosses from C# to a spreadsheet
    /// a person reads, not to JSON a screen renders. A role an operator
    /// cannot already read on screen is a role a spreadsheet should not
    /// invent its own name for.
    /// </summary>
    public static readonly IReadOnlyList<(StaffRole Role, string Label)> RoleLabels =
    [
        (StaffRole.HeadCoach, "Director técnico"),
        (StaffRole.AssistantCoach, "Asistente técnico"),
        (StaffRole.PhysicalTrainer, "Preparador físico"),
        (StaffRole.Physician, "Médico"),
        (StaffRole.Physiotherapist, "Kinesiólogo"),
        (StaffRole.Delegate, "Delegado"),
        (StaffRole.Director, "Dirigente"),
    ];

    /// <summary>
    /// The role a label names, matched the same forgiving way a header is —
    /// accents, case and stray spaces stripped before comparing, because a
    /// workbook has been through Excel and somebody's phone by the time it
    /// comes back. Null if the text names none of them.
    /// </summary>
    public static StaffRole? RoleOf(string? label)
    {
        if (label is null)
        {
            return null;
        }

        var normalized = RosterSheet.Normalize(label);

        foreach (var (role, candidate) in RoleLabels)
        {
            if (RosterSheet.Normalize(candidate) == normalized)
            {
                return role;
            }
        }

        return null;
    }
}
