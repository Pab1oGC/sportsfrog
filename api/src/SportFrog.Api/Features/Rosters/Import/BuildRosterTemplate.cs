using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Hands out the spreadsheet a club fills in to register a squad.
/// </summary>
/// <remarks>
/// Generated for one team rather than offered as a blank download, and that
/// is the whole point of the endpoint. A generic template needs columns for
/// the club and the division, which means every row carries a chance to name
/// the wrong one, and a misspelt club creates a club. Handed out from the
/// team, the file already knows where it is going and the person filling it
/// in has one less thing to get right.
///
/// It also carries the category's own rules — the sex it admits, the birth
/// dates it takes — as validation Excel enforces while somebody types. That
/// is the difference between learning about an ineligible player now and
/// learning about them after forty have been typed in.
/// </remarks>
public static class BuildRosterTemplate
{
    public static IEndpointRouteBuilder MapBuildRosterTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/teams/{teamId:guid}/roster/import/template", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(BuildRosterTemplate))
            .WithSummary("Builds the squad spreadsheet for a team, carrying its category's rules.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var team = await database.Teams
            .AsNoTracking()
            .Include(candidate => candidate.Category)
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team?.Category is not { } category)
        {
            return Results.NotFound();
        }

        using var workbook = new XLWorkbook();

        Compose(workbook, category);
        Stamp(workbook, organization.RequireOrganizationId(), team, category);

        using var file = new MemoryStream();
        workbook.SaveAs(file);

        return Results.File(
            file.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(team, category));
    }

    /// <summary>Lays out the sheet somebody types into.</summary>
    private static void Compose(XLWorkbook workbook, Category category)
    {
        var sheet = workbook.AddWorksheet(RosterSheet.DataSheet);
        var columns = Columns(category);

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var position = index + 1;

            var heading = sheet.Cell(RosterSheet.HeaderRow, position);
            heading.Value = column.Header;
            heading.Style.Font.Bold = true;
            heading.Style.Fill.BackgroundColor =
                column.Required ? XLColor.LightGreen : XLColor.WhiteSmoke;
            heading.Style.Alignment.WrapText = true;

            var body = sheet.Range(
                RosterSheet.FirstDataRow, position, RosterSheet.MaximumRows + 1, position);

            // The rule sits where the typing is. A tab of instructions is a
            // tab nobody opens.
            var validation = body.CreateDataValidation();
            validation.ShowInputMessage = true;
            validation.InputTitle = column.Header;
            validation.InputMessage = Hint(column, category);

            Constrain(validation, column, category);
        }

        // The document is text, so 0071 keeps its leading zero and a long one
        // does not turn into scientific notation. Both are how identity
        // numbers get quietly corrupted in spreadsheets.
        sheet.Column(columns.IndexOf(RosterSheet.Document) + 1).Style.NumberFormat.Format = "@";
        sheet.Column(columns.IndexOf(RosterSheet.BirthDate) + 1).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.SheetView.FreezeRows(RosterSheet.HeaderRow);
        sheet.Columns().AdjustToContents(1, 1, 12d, 26d);
    }

    /// <summary>
    /// The columns this category's template gets. The full set, minus the
    /// guardian's name and phone when nobody it admits can be a minor.
    /// </summary>
    /// <remarks>
    /// Those two columns exist for the categories that need them: a squad of
    /// children needs a parent's name on file for each one. A category whose
    /// birth-date floor already sits eighteen years back cannot produce a
    /// minor at all, and handing its operator a spreadsheet with two columns
    /// that never apply to anyone is not a safeguard kept just in case — it
    /// is two blank columns they now have to notice are irrelevant and leave
    /// alone, on every single row.
    ///
    /// The reader does not need telling: both columns were already optional
    /// there, so a template that omits them is simply a workbook nobody
    /// filled them in on, which is exactly what an adult squad already looks
    /// like today.
    /// </remarks>
    private static List<SheetColumn> Columns(Category category) =>
        OnlyAdmitsAdults(category)
            ? RosterSheet.Columns
                .Where(column => column != RosterSheet.Guardian && column != RosterSheet.GuardianPhone)
                .ToList()
            : [.. RosterSheet.Columns];

    /// <summary>
    /// Whether every athlete this category could possibly admit is already
    /// an adult, going by its own birth-date floor.
    /// </summary>
    /// <remarks>
    /// The floor is <see cref="Category.BirthDateTo"/>: the latest birth date
    /// the category takes, which is what bounds how young a player may be. No
    /// floor at all means no such promise was made — the category might still
    /// take a child — so guardian columns stay unless the floor itself rules
    /// that out.
    /// </remarks>
    private static bool OnlyAdmitsAdults(Category category) =>
        category.BirthDateTo is { } latest
        && latest <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18);

    /// <summary>
    /// Turns the category's rules into something Excel refuses as it is typed.
    /// </summary>
    /// <remarks>
    /// A warning rather than a refusal: the spreadsheet is a convenience and
    /// the system revalidates everything on the way back in, so a rule that
    /// locked somebody out of their own file would cost more than it saved.
    /// </remarks>
    private static void Constrain(
        IXLDataValidation validation,
        SheetColumn column,
        Category category)
    {
        validation.IgnoreBlanks = true;
        validation.ShowErrorMessage = true;
        validation.ErrorStyle = XLErrorStyle.Warning;
        validation.ErrorTitle = column.Header;

        if (column == RosterSheet.Sex)
        {
            // Only the value the category admits, where it admits one. A
            // dropdown with a single option looks odd and is exactly right:
            // there is one answer.
            validation.List(category.Gender is { } admitted ? $"\"{admitted}\"" : "\"F,M\"");

            validation.ErrorMessage = category.Gender is { } only
                ? $"Esta categoría admite solo {only}."
                : "Sexo es F o M.";
        }
        else if (column == RosterSheet.BirthDate)
        {
            var earliest = category.BirthDateFrom?.ToDateTime(TimeOnly.MinValue)
                ?? new DateTime(1900, 1, 1);
            var latest = category.BirthDateTo?.ToDateTime(TimeOnly.MinValue)
                ?? DateTime.UtcNow.Date;

            validation.Date.Between(earliest, latest);

            validation.ErrorMessage = Window(category)
                ?? "La fecha de nacimiento tiene que ser una fecha real y anterior a hoy.";
        }
        else if (column == RosterSheet.Jersey)
        {
            validation.WholeNumber.Between(0, 999);
            validation.ErrorMessage = "El dorsal es un número entre 0 y 999.";
        }
        else
        {
            validation.ShowErrorMessage = false;
        }
    }

    /// <summary>
    /// The note shown when a cell is selected, carrying the category's rule
    /// where it has one.
    /// </summary>
    private static string Hint(SheetColumn column, Category category)
    {
        if (column == RosterSheet.Sex && category.Gender is { } admitted)
        {
            return $"Esta categoría admite solo {admitted}, así que la columna es obligatoria.";
        }

        if (column == RosterSheet.BirthDate && Window(category) is { } window)
        {
            return $"{column.Hint} {window}";
        }

        return column.Hint;
    }

    /// <summary>The birth dates the category takes, worded for a person.</summary>
    private static string? Window(Category category) => category switch
    {
        { BirthDateFrom: { } from, BirthDateTo: { } to } =>
            $"Admite nacidos entre {from:yyyy-MM-dd} y {to:yyyy-MM-dd}.",
        { BirthDateFrom: { } from } => $"Admite nacidos desde {from:yyyy-MM-dd}.",
        { BirthDateTo: { } to } => $"Admite nacidos hasta {to:yyyy-MM-dd}.",
        _ => null,
    };

    /// <summary>
    /// Writes down which team this workbook was made for.
    /// </summary>
    /// <remarks>
    /// Very hidden rather than merely hidden, so it survives somebody tidying
    /// up the tabs they can see. It exists for one failure, and it is the
    /// worst one this feature has: a file filled in for one team and uploaded
    /// against another, which registers forty children for the wrong club and
    /// looks from the outside like it worked.
    /// </remarks>
    private static void Stamp(
        XLWorkbook workbook,
        Guid organizationId,
        Team team,
        Category category)
    {
        var marker = workbook.AddWorksheet(RosterSheet.MarkerSheet);

        marker.Cell(RosterSheet.MarkerNote, 1).Value =
            "No modificar. Identifica el equipo para el que se generó esta planilla.";
        marker.Cell(RosterSheet.MarkerVersion, 1).Value = RosterSheet.Version;
        marker.Cell(RosterSheet.MarkerOrganization, 1).Value = organizationId.ToString();
        marker.Cell(RosterSheet.MarkerTeam, 1).Value = team.Id.ToString();
        marker.Cell(RosterSheet.MarkerSubject, 1).Value = $"{team.Name} - {category.Name}";

        marker.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    /// <summary>
    /// Named after the team, because an operator downloads one of these per
    /// club and they all land in the same folder.
    /// </summary>
    private static string FileName(Team team, Category category)
    {
        var readable = string.Concat($"{team.Name}-{category.Name}"
                .Normalize(NormalizationForm.FormD)
                .Where(character =>
                    char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"planilla-{(readable.Length > 0 ? readable : "equipo")}.xlsx";
    }
}
