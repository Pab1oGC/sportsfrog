using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Registers the delegation's athletes a spreadsheet describes, each into
/// the category their own row names.
/// </summary>
/// <remarks>
/// A sibling of <see cref="ApplyRosterImport"/>: re-reviews before applying
/// for the same reason (the world moved since the preview was shown), and
/// lands the whole file in one transaction for the same reason (a squad
/// half entered is a worse state than any squad). The real difference is
/// what an accepted row creates — that one adds a roster entry to a team
/// that already exists; every accepted row here creates its own team of
/// one first, because a delegation entering an individual sport is entering
/// several separate competitors, not one squad.
/// </remarks>
public static class ApplyDelegationRosterImport
{
    private const long MaximumUpload = 5 * 1024 * 1024;

    public sealed record Row(
        int Number,
        string? Document,
        string Name,
        string? Category,
        ImportOutcome Outcome,
        IReadOnlyList<string> Problems);

    public sealed record Response(
        Guid CompetitionId,
        string CompetitionName,
        Guid ClubId,
        string ClubName,
        int Total,
        int Registered,
        int Created,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapApplyDelegationRosterImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
                "/competitions/{competitionId:guid}/clubs/{clubId:guid}/individuals/import",
                HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(ApplyDelegationRosterImport))
            .WithSummary("Registers the delegation's athletes a filled-in spreadsheet describes.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid clubId,
        IFormFile file,
        SportFrogDbContext database,
        DelegationRosterImportReview review,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await DelegationRosterImportGate.OpenAsync(
                competitionId, clubId, file, MaximumUpload, database, cancellationToken)
            is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var (competition, club, categories, sheet) =
            (opened.Competition!, opened.Club!, opened.Categories!, opened.Sheet!);

        var reviewed = await review.ReviewAsync(competitionId, club, categories, sheet.Rows, cancellationToken);

        var organizationId = organization.RequireOrganizationId();
        var byDocument = sheet.Rows
            .Where(row => row.Document is not null)
            .GroupBy(row => row.Document!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // Everyone the file names who is already on the register, read once.
        // The rows that create somebody are the ones this does not find.
        var known = await database.Athletes
            .Where(athlete => byDocument.Keys.Contains(athlete.DocumentId))
            .ToDictionaryAsync(
                athlete => athlete.DocumentId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var accepted in reviewed.Rows.Where(row =>
            row.Outcome is ImportOutcome.Register or ImportOutcome.CreateAndRegister))
        {
            var row = byDocument[accepted.Document!];
            var category = categories.Single(candidate => candidate.Id == accepted.CategoryId);

            var athlete = known.GetValueOrDefault(accepted.Document!) ?? Create(row, organizationId);

            if (!known.ContainsKey(athlete.DocumentId))
            {
                database.Athletes.Add(athlete);
                known[athlete.DocumentId] = athlete;
            }

            var team = new Team
            {
                Id = Guid.NewGuid(),
                OrgId = organizationId,
                ClubId = club.Id,
                CategoryId = category.Id,
                Name = accepted.Name,
                IsIndividual = true,
            };

            database.Teams.Add(team);

            database.RosterEntries.Add(new RosterEntry
            {
                Id = Guid.NewGuid(),
                OrgId = organizationId,
                TeamId = team.Id,
                AthleteId = athlete.Id,
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
            // Somebody enrolled one of these athletes through the interface,
            // or through another upload, at the same moment. The review
            // answered a question that was true when it was asked; the
            // indexes are what settle a tie.
            return Results.Problem(
                detail: "Alguien inscribió a uno de estos deportistas mientras se aplicaba el "
                        + "archivo. Hay que volver a subirlo: nada de lo que traía quedó guardado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new Response(
            competition.Id,
            competition.Name,
            club.Id,
            club.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.CategoryName, row.Outcome, row.Problems))]));
    }

    /// <summary>
    /// The person a row describes, as they go onto the register. Mirrors
    /// <see cref="ApplyRosterImport.Create"/> exactly — RF-43 is the same
    /// fact here as there: the document is the identity, so this same person
    /// entered again next season attaches to this same record.
    /// </summary>
    private static Athlete Create(DelegationSheetRow row, Guid organizationId) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = organizationId,
        FirstName = row.FirstName!,
        LastName = row.LastName!,
        DocumentId = row.Document!,
        BirthDate = row.BirthDate!.Value,
        Gender = Sex.Normalize(row.Sex),
        GuardianName = row.Guardian,
        GuardianPhone = row.GuardianPhone,
    };
}
