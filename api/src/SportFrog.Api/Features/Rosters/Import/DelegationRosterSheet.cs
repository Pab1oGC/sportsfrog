namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>One line of the delegation's squad sheet, as it was typed.</summary>
internal sealed record DelegationSheetRow(
    int Number,
    string? Document,
    string? LastName,
    string? FirstName,
    DateOnly? BirthDate,
    string? BirthDateText,
    string? Sex,
    decimal? Weight,
    string? WeightText,
    string? CategoryName,
    string? Guardian,
    string? GuardianPhone);

/// <summary>What a delegation's workbook turned out to contain.</summary>
/// <param name="Problem">Something wrong with the file as a whole. Null when it was read.</param>
/// <param name="StampedCompetition">The competition the template was generated for, if it says.</param>
/// <param name="StampedClub">The club the template was generated for, if it says.</param>
internal sealed record DelegationSheetContents(
    string? Problem,
    Guid? StampedCompetition,
    Guid? StampedClub,
    string? StampedSubject,
    IReadOnlyList<DelegationSheetRow> Rows);

/// <summary>
/// The shape of the sheet a delegation fills in to enter its athletes into
/// an individual-sport competition — one row per athlete, each choosing its
/// own category, since a delegation enters competitors across every weight
/// class it fields at once rather than one squad into one division.
/// </summary>
/// <remarks>
/// A sibling of <see cref="RosterSheet"/>, not a variant of it. That one is
/// generated for a team already inside one category, so it carries no
/// column for one and instead carries jersey and position, which describe a
/// squad sport this is not. Kept as its own definition, read by its own
/// reader, so that neither sheet's shape can be changed by a fix meant for
/// the other — the team-scoped importer this sits beside has no test suite
/// of its own to catch that kind of accident.
///
/// <see cref="SportFrog.Api.Features.Rosters.Import.RosterSheet.Normalize"/>
/// is reused as-is for matching headers and category names: it has no team
/// semantics of its own, so sharing it costs nothing.
/// </remarks>
internal static class DelegationRosterSheet
{
    public const string DataSheet = "Deportistas";

    /// <summary>Where the competition and club's identity is stamped, out of the way.</summary>
    public const string MarkerSheet = "sportfrog";

    public const int Version = 2;

    public const int HeaderRow = 1;

    public const int FirstDataRow = 2;

    public const int MaximumRows = 1000;

    public static readonly SheetColumn Document =
        new("Documento", "Documento de identidad. Es lo que identifica a la persona: si ya está registrada, se la reutiliza.", Required: true);

    public static readonly SheetColumn LastName =
        new("Apellidos", "Apellidos, como figuran en el documento.", Required: true);

    public static readonly SheetColumn FirstName =
        new("Nombres", "Nombres, como figuran en el documento.", Required: true);

    public static readonly SheetColumn BirthDate =
        new("Fecha de nacimiento", "Fecha, no texto. La categoría elegida admite un rango de fechas y se compara contra esto.", Required: true);

    public static readonly SheetColumn Sex =
        new("Sexo", "F o M. Obligatorio solo si la categoría elegida admite uno de los dos.", Required: false);

    public static readonly SheetColumn Weight =
        new("Peso (kg)", "Peso más reciente, en kilogramos. Obligatorio solo si la categoría elegida admite un rango de peso.", Required: false);

    public static readonly SheetColumn Category =
        new("Categoría", "En qué categoría de esta competencia compite. Tiene que ser una de las de la lista, escrita igual.", Required: true);

    public static readonly SheetColumn Guardian =
        new("Apoderado", "Nombre del padre, madre o tutor. Necesario para menores.", Required: false);

    public static readonly SheetColumn GuardianPhone =
        new("Teléfono del apoderado", "Teléfono de contacto del apoderado.", Required: false);

    /// <summary>The columns, in the order the template lays them out.</summary>
    /// <remarks>
    /// No jersey, no position: neither means anything for an individual
    /// sport, and a column the system does not read is worse than no
    /// column — somebody fills it in believing it did something. Weight is
    /// the opposite case: several individual sports (taekwondo among them)
    /// draw their categories along a weight window, so leaving it off would
    /// make every row that creates a new athlete fail that category's check
    /// for a reason the sheet gave no way to fix.
    /// </remarks>
    public static readonly IReadOnlyList<SheetColumn> Columns =
    [
        Document, LastName, FirstName, BirthDate, Sex, Weight, Category, Guardian, GuardianPhone,
    ];

    /// <summary>Rows of the hidden sheet that says what this workbook is.</summary>
    public const int MarkerNote = 1;
    public const int MarkerVersion = 2;
    public const int MarkerOrganization = 3;
    public const int MarkerCompetition = 4;
    public const int MarkerClub = 5;
    public const int MarkerSubject = 6;
}
