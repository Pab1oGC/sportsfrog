using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Hands out the spreadsheet a delegation fills in to enter its athletes
/// into an individual-sport competition.
/// </summary>
/// <remarks>
/// A sibling of <see cref="BuildRosterTemplate"/>, not a variant: that one is
/// generated for a team already inside one category, and its columns —
/// jersey, position — describe a squad. This one is generated for a club
/// entering athletes across every category of one competition at once, so
/// each row picks its own category from a list this file offers, instead of
/// the whole file being scoped to one.
/// </remarks>
public static class BuildDelegationRosterTemplate
{
    public static IEndpointRouteBuilder MapBuildDelegationRosterTemplate(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(
                "/competitions/{competitionId:guid}/clubs/{clubId:guid}/individuals/import/template",
                HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(BuildDelegationRosterTemplate))
            .WithSummary("Builds the squad spreadsheet for a delegation entering an individual sport.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid clubId,
        SportFrogDbContext database,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions
            .AsNoTracking()
            .Include(candidate => candidate.Sport)
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition?.Sport is not { IsIndividual: true })
        {
            return Results.NotFound();
        }

        var club = await database.Clubs
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == clubId, cancellationToken);

        if (club is null)
        {
            return Results.NotFound();
        }

        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => category.CompetitionId == competitionId)
            .OrderBy(category => category.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
        {
            return Results.Problem(
                detail: "Esta competencia todavía no tiene categorías, así que no hay dónde "
                        + "anotar a nadie.",
                statusCode: StatusCodes.Status409Conflict);
        }

        using var workbook = new XLWorkbook();

        var collectsWeight = RosterSheet.CollectsWeight(
            competition.SportCode,
            categories.Any(category => category.MinWeightKg is not null || category.MaxWeightKg is not null));

        Compose(workbook, categories, collectsWeight);
        Stamp(workbook, organization.RequireOrganizationId(), competition, club);

        using var file = new MemoryStream();
        workbook.SaveAs(file);

        return Results.File(
            file.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(competition, club));
    }

    /// <summary>Lays out the sheet somebody types into.</summary>
    private static void Compose(XLWorkbook workbook, IReadOnlyList<Category> categories, bool collectsWeight)
    {
        var sheet = workbook.AddWorksheet(DelegationRosterSheet.DataSheet);
        var columns = DelegationRosterSheet.Columns
            .Where(column => collectsWeight || column != DelegationRosterSheet.Weight)
            .ToList();

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var position = index + 1;

            var heading = sheet.Cell(DelegationRosterSheet.HeaderRow, position);
            heading.Value = column.Header;
            heading.Style.Font.Bold = true;
            heading.Style.Fill.BackgroundColor =
                column.Required ? XLColor.LightGreen : XLColor.WhiteSmoke;
            heading.Style.Alignment.WrapText = true;

            var body = sheet.Range(
                DelegationRosterSheet.FirstDataRow, position,
                DelegationRosterSheet.MaximumRows + 1, position);

            var validation = body.CreateDataValidation();
            validation.ShowInputMessage = true;
            validation.InputTitle = column.Header;
            validation.InputMessage = column.Hint;

            Constrain(validation, column, categories);
        }

        // The document is text, so 0071 keeps its leading zero and a long
        // one does not turn into scientific notation.
        sheet.Column(columns.IndexOf(DelegationRosterSheet.Document) + 1).Style.NumberFormat.Format = "@";
        sheet.Column(columns.IndexOf(DelegationRosterSheet.BirthDate) + 1).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.SheetView.FreezeRows(DelegationRosterSheet.HeaderRow);
        sheet.Columns().AdjustToContents(1, 1, 12d, 26d);
    }

    /// <summary>
    /// Turns what can be said ahead of time into something Excel refuses as
    /// it is typed.
    /// </summary>
    /// <remarks>
    /// A warning rather than a refusal, same as <see cref="BuildRosterTemplate"/>:
    /// the spreadsheet is a convenience and the system revalidates everything
    /// on the way back in. Sex and birth date cannot be checked against a
    /// specific category's window here the way that template does — a row
    /// does not know which category it is for until the Categoría cell is
    /// filled in, and this file spans every category the competition has,
    /// each with its own window. RosterPolicy still enforces all of it once
    /// the category is read back.
    /// </remarks>
    private static void Constrain(
        IXLDataValidation validation,
        SheetColumn column,
        IReadOnlyList<Category> categories)
    {
        validation.IgnoreBlanks = true;
        validation.ShowErrorMessage = true;
        validation.ErrorStyle = XLErrorStyle.Warning;
        validation.ErrorTitle = column.Header;

        if (column == DelegationRosterSheet.Sex)
        {
            validation.List("\"F,M\"");
            validation.ErrorMessage = "Sexo es F o M.";
        }
        else if (column == DelegationRosterSheet.BirthDate)
        {
            validation.Date.Between(new DateTime(1900, 1, 1), DateTime.UtcNow.Date);
            validation.ErrorMessage = "La fecha de nacimiento tiene que ser una fecha real y anterior a hoy.";
        }
        else if (column == DelegationRosterSheet.Category)
        {
            validation.List($"\"{string.Join(',', categories.Select(category => category.Name))}\"");
            validation.ErrorMessage = "Tiene que ser una de las categorías de esta competencia.";
        }
        else
        {
            validation.ShowErrorMessage = false;
        }
    }

    /// <summary>
    /// Writes down which competition and club this workbook was made for.
    /// </summary>
    /// <remarks>
    /// Very hidden rather than merely hidden, so it survives somebody
    /// tidying up the tabs they can see — see
    /// <see cref="BuildRosterTemplate.Stamp"/>, which this mirrors for the
    /// same reason: a file filled in for one delegation and uploaded against
    /// another registers its athletes under the wrong club and looks, from
    /// the outside, like it worked.
    /// </remarks>
    private static void Stamp(
        XLWorkbook workbook,
        Guid organizationId,
        Competition competition,
        Club club)
    {
        var marker = workbook.AddWorksheet(DelegationRosterSheet.MarkerSheet);

        marker.Cell(DelegationRosterSheet.MarkerNote, 1).Value =
            "No modificar. Identifica la competencia y el club para los que se generó esta planilla.";
        marker.Cell(DelegationRosterSheet.MarkerVersion, 1).Value = DelegationRosterSheet.Version;
        marker.Cell(DelegationRosterSheet.MarkerOrganization, 1).Value = organizationId.ToString();
        marker.Cell(DelegationRosterSheet.MarkerCompetition, 1).Value = competition.Id.ToString();
        marker.Cell(DelegationRosterSheet.MarkerClub, 1).Value = club.Id.ToString();
        marker.Cell(DelegationRosterSheet.MarkerSubject, 1).Value = $"{competition.Name} - {club.Name}";

        marker.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    private static string FileName(Competition competition, Club club)
    {
        var readable = string.Concat($"{competition.Name}-{club.Name}"
                .Normalize(NormalizationForm.FormD)
                .Where(character =>
                    char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"nomina-{(readable.Length > 0 ? readable : "delegacion")}.xlsx";
    }
}
