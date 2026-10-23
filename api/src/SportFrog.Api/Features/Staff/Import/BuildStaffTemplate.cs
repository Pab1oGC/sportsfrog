using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>
/// Hands out the spreadsheet a club fills in to register a team's technical
/// staff and board.
/// </summary>
/// <remarks>
/// A sibling of <see cref="BuildRosterTemplate"/>, generated for one team for
/// the same reason: a generic template needs a column naming the team, which
/// is one more chance to misspell it, and handing it out from the team means
/// the file already knows where it is going. No category rules travel with
/// this one the way a squad's do — <see cref="StaffPolicy"/> never reads a
/// category at all — so there is nothing here to constrain beyond the cargo
/// itself.
/// </remarks>
public static class BuildStaffTemplate
{
    public static IEndpointRouteBuilder MapBuildStaffTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/teams/{teamId:guid}/staff/import/template", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.Team, "teamId"))
            .WithName(nameof(BuildStaffTemplate))
            .WithSummary("Builds the technical staff spreadsheet for a team.");

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
            .SingleOrDefaultAsync(candidate => candidate.Id == teamId, cancellationToken);

        if (team is null)
        {
            return Results.NotFound();
        }

        using var workbook = new XLWorkbook();

        Compose(workbook);
        Stamp(workbook, organization.RequireOrganizationId(), team);

        using var file = new MemoryStream();
        workbook.SaveAs(file);

        return Results.File(
            file.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(team));
    }

    /// <summary>Lays out the sheet somebody types into.</summary>
    private static void Compose(XLWorkbook workbook)
    {
        var sheet = workbook.AddWorksheet(StaffSheet.DataSheet);
        var columns = StaffSheet.Columns.ToList();

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var position = index + 1;

            var heading = sheet.Cell(StaffSheet.HeaderRow, position);
            heading.Value = column.Header;
            heading.Style.Font.Bold = true;
            heading.Style.Fill.BackgroundColor =
                column.Required ? XLColor.LightGreen : XLColor.WhiteSmoke;
            heading.Style.Alignment.WrapText = true;

            var body = sheet.Range(
                StaffSheet.FirstDataRow, position, StaffSheet.MaximumRows + 1, position);

            // The rule sits where the typing is, same as BuildRosterTemplate.
            var validation = body.CreateDataValidation();
            validation.ShowInputMessage = true;
            validation.InputTitle = column.Header;
            validation.InputMessage = column.Hint;

            Constrain(validation, column);
        }

        // Text, so a document keeps its leading zero and a long one does not
        // turn into scientific notation.
        sheet.Column(columns.IndexOf(StaffSheet.Document) + 1).Style.NumberFormat.Format = "@";
        sheet.Column(columns.IndexOf(StaffSheet.BirthDate) + 1).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.SheetView.FreezeRows(StaffSheet.HeaderRow);
        sheet.Columns().AdjustToContents(1, 1, 12d, 30d);
    }

    /// <summary>
    /// Turns what can be said ahead of time into something Excel refuses as
    /// it is typed. A warning rather than a refusal, same as
    /// <see cref="BuildRosterTemplate"/>: the spreadsheet is a convenience,
    /// and <see cref="StaffImportReview"/> revalidates everything on the way
    /// back in.
    /// </summary>
    private static void Constrain(IXLDataValidation validation, SheetColumn column)
    {
        validation.IgnoreBlanks = true;
        validation.ShowErrorMessage = true;
        validation.ErrorStyle = XLErrorStyle.Warning;
        validation.ErrorTitle = column.Header;

        if (column == StaffSheet.BirthDate)
        {
            validation.Date.Between(new DateTime(1900, 1, 1), DateTime.UtcNow.Date);
            validation.ErrorMessage = "La fecha de nacimiento tiene que ser una fecha real y anterior a hoy.";
        }
        else if (column == StaffSheet.Role)
        {
            // Only the cargos the system knows, in the same words StaffPanel
            // already shows on screen.
            validation.List($"\"{string.Join(',', StaffSheet.RoleLabels.Select(entry => entry.Label))}\"");
            validation.ErrorMessage = "Tiene que ser uno de los cargos de la lista.";
        }
        else
        {
            validation.ShowErrorMessage = false;
        }
    }

    /// <summary>
    /// Writes down which team this workbook was made for — very hidden, same
    /// as <see cref="BuildRosterTemplate.Stamp"/> and for the same reason: a
    /// file filled in for one team and uploaded against another registers a
    /// bench under the wrong club and looks, from the outside, like it
    /// worked.
    /// </summary>
    private static void Stamp(XLWorkbook workbook, Guid organizationId, Team team)
    {
        var marker = workbook.AddWorksheet(StaffSheet.MarkerSheet);

        marker.Cell(StaffSheet.MarkerNote, 1).Value =
            "No modificar. Identifica el equipo para el que se generó esta planilla.";
        marker.Cell(StaffSheet.MarkerVersion, 1).Value = StaffSheet.Version;
        marker.Cell(StaffSheet.MarkerOrganization, 1).Value = organizationId.ToString();
        marker.Cell(StaffSheet.MarkerTeam, 1).Value = team.Id.ToString();
        marker.Cell(StaffSheet.MarkerSubject, 1).Value = $"{team.Name} (cuerpo técnico)";

        marker.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    private static string FileName(Team team)
    {
        var readable = string.Concat(team.Name
                .Normalize(NormalizationForm.FormD)
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"cuerpo-tecnico-{(readable.Length > 0 ? readable : "equipo")}.xlsx";
    }
}
