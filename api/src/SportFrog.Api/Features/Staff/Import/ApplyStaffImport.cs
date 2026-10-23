using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>
/// Registers the technical staff a filled-in spreadsheet describes.
/// </summary>
/// <remarks>
/// The staff counterpart of <see cref="ApplyRosterImport"/>: re-reviews
/// before applying for the same reason (the world moved since the preview
/// was shown — somebody else may have registered a head coach in the
/// meantime), and lands the whole file in one transaction for the same
/// reason (a bench half entered is a worse state than any bench).
/// </remarks>
public static class ApplyStaffImport
{
    private const long MaximumUpload = 5 * 1024 * 1024;

    public sealed record Row(
        int Number,
        string? Document,
        string Name,
        string? Role,
        ImportOutcome Outcome,
        IReadOnlyList<string> Problems);

    public sealed record Response(
        Guid TeamId,
        string TeamName,
        int Total,
        int Registered,
        int Created,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapApplyStaffImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/staff/import", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.Team, "teamId"))
            .DisableAntiforgery()
            .WithName(nameof(ApplyStaffImport))
            .WithSummary("Registers the technical staff a filled-in spreadsheet describes.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        IFormFile file,
        SportFrogDbContext database,
        StaffImportReview review,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await StaffImportGate.OpenAsync(teamId, file, MaximumUpload, database, cancellationToken)
            is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var (team, sheet) = (opened.Team!, opened.Sheet!);

        var reviewed = await review.ReviewAsync(team, sheet.Rows, cancellationToken);

        var organizationId = organization.RequireOrganizationId();
        var byDocument = sheet.Rows
            .Where(row => row.Document is not null)
            .GroupBy(row => row.Document!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // Everyone the file names who is already in the organization's
        // people, read once. The rows that create somebody are the ones
        // this does not find.
        var known = await database.Athletes
            .Where(athlete => byDocument.Keys.Contains(athlete.DocumentId))
            .ToDictionaryAsync(
                athlete => athlete.DocumentId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var accepted in reviewed.Rows.Where(row =>
            row.Outcome is ImportOutcome.Register or ImportOutcome.CreateAndRegister))
        {
            var row = byDocument[accepted.Document!];

            var athlete = known.GetValueOrDefault(accepted.Document!) ?? Create(row, organizationId);

            if (!known.ContainsKey(athlete.DocumentId))
            {
                database.Athletes.Add(athlete);
                known[athlete.DocumentId] = athlete;
            }

            database.TeamStaff.Add(new TeamStaff
            {
                Id = Guid.NewGuid(),
                OrgId = organizationId,
                TeamId = team.Id,
                AthleteId = athlete.Id,

                // Already validated non-null by the review that just ran:
                // every accepted row named a recognized cargo.
                Role = row.Role!.Value,
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Somebody registering staff through the interface, or through
            // another upload, at the same moment. The review answered a
            // question that was true when it was asked; the indexes are
            // what settle a tie.
            return Results.Problem(
                detail: "Alguien registró a una de estas personas en el cuerpo técnico mientras se "
                        + "aplicaba el archivo. Hay que volver a subirlo: nada de lo que traía quedó "
                        + "guardado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new Response(
            team.Id,
            team.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.Role, row.Outcome, row.Problems))]));
    }

    /// <summary>
    /// The person a row describes, as they go onto the register. Mirrors
    /// <see cref="ApplyRosterImport.Create"/>: the document is the identity,
    /// so this same person entered again next season attaches to this same
    /// record (RF-43).
    /// </summary>
    private static Athlete Create(StaffSheetRow row, Guid organizationId) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = organizationId,
        FirstName = row.FirstName!,
        LastName = row.LastName!,
        DocumentId = row.Document!,
        BirthDate = row.BirthDate!.Value,
    };
}
