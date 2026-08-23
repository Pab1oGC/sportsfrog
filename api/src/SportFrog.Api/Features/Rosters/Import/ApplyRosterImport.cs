using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Rosters.Import;

/// <summary>
/// Registers the squad a spreadsheet describes.
/// </summary>
/// <remarks>
/// The confirmation of what the preview showed, and it reads the file again
/// rather than trusting what that preview said. It has to: the preview was
/// answered minutes ago and the world moved — somebody registered a player
/// for a rival, a shirt was taken, the squad filled up. Applying a stale
/// verdict is how an import ends up doing something nobody was shown.
///
/// So it re-reviews and applies what the fresh review accepts, and the
/// response says exactly what happened, in the same shape as the preview. If
/// something changed in between, the operator sees it there.
///
/// Not a background job, deliberately. The slow thing in this module is
/// images, and there are none here: this is a few hundred inserts, which the
/// database does in the time it takes to describe them. Putting it on a queue
/// would buy nothing and cost the one property that matters most — the whole
/// squad lands or none of it does, in one transaction, which is exactly what
/// having a preview is for.
/// </remarks>
public static class ApplyRosterImport
{
    private const long MaximumUpload = 5 * 1024 * 1024;

    public sealed record Row(
        int Number,
        string? Document,
        string Name,
        ImportOutcome Outcome,
        IReadOnlyList<string> Problems);

    public sealed record Response(
        Guid TeamId,
        string TeamName,
        string CategoryName,
        int Total,
        int Registered,
        int Created,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapApplyRosterImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/roster/import", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(ApplyRosterImport))
            .WithSummary("Registers the squad a filled-in spreadsheet describes.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        IFormFile file,
        SportFrogDbContext database,
        RosterImportReview review,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        if (await RosterImportGate.OpenAsync(teamId, file, MaximumUpload, database, cancellationToken)
            is not { } opened)
        {
            return Results.NotFound();
        }

        if (opened.Refusal is { } refusal)
        {
            return refusal;
        }

        var (team, category, sheet) = (opened.Team!, opened.Category!, opened.Sheet!);

        var reviewed = await review.ReviewAsync(team, category, sheet.Rows, cancellationToken);

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

            var athlete = known.GetValueOrDefault(accepted.Document!) ?? Create(row, organizationId);

            if (!known.ContainsKey(athlete.DocumentId))
            {
                database.Athletes.Add(athlete);
                known[athlete.DocumentId] = athlete;
            }

            database.RosterEntries.Add(new RosterEntry
            {
                Id = Guid.NewGuid(),
                OrgId = organizationId,
                TeamId = team.Id,
                AthleteId = athlete.Id,
                JerseyNumber = row.Jersey,
                Position = row.Position,
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
            // Somebody registering a player through the interface at the same
            // moment. The review answered a question that was true when it was
            // asked; the indexes are what settle a tie.
            return Results.Problem(
                detail: "Alguien inscribió a un jugador en este equipo mientras se aplicaba el "
                        + "archivo. Hay que volver a subirlo: nada de lo que traía quedó guardado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(new Response(
            team.Id,
            team.Name,
            category.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.Outcome, row.Problems))]));
    }

    /// <summary>
    /// The person a row describes, as they go onto the register.
    /// </summary>
    /// <remarks>
    /// Registered once and reused afterwards, which is the whole of RF-43: the
    /// document is the identity, so next season's spreadsheet attaches to the
    /// same person and their history comes with them instead of starting again
    /// under a second record.
    ///
    /// No photograph. Those arrive in their own archive and are matched by
    /// this same document.
    /// </remarks>
    private static Athlete Create(SheetRow row, Guid organizationId) => new()
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
