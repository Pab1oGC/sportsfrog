using SportFrog.Api.Features.Rosters.Import;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Staff.Import;

/// <summary>
/// Reads a filled-in staff sheet and says what importing it would do.
/// </summary>
/// <remarks>
/// The staff counterpart of <see cref="PreviewRosterImport"/>: writes
/// nothing, for the same reason that one does not — the useful moment to
/// learn row 6 names an unrecognized cargo is before any of the file is in
/// the database.
/// </remarks>
public static class PreviewStaffImport
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
        int ToRegister,
        int ToCreate,
        int AlreadyRegistered,
        int Rejected,
        IReadOnlyList<Row> Rows);

    public static IEndpointRouteBuilder MapPreviewStaffImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/teams/{teamId:guid}/staff/import/preview", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Club, ScopeTargetKind.Team, "teamId"))

            // Bearer token, not a cookie: there is no session for another
            // site to ride on, and left on this would refuse every upload.
            .DisableAntiforgery()
            .WithName(nameof(PreviewStaffImport))
            .WithSummary("Reads a filled-in staff sheet and reports what importing it would do.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid teamId,
        IFormFile file,
        SportFrogDbContext database,
        StaffImportReview review,
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

        var reviewed = await review.ReviewAsync(opened.Team!, opened.Sheet!.Rows, cancellationToken);

        return Results.Ok(new Response(
            opened.Team!.Id,
            opened.Team.Name,
            reviewed.Rows.Count,
            reviewed.Register,
            reviewed.Create,
            reviewed.AlreadyRegistered,
            reviewed.Rejected,
            [.. reviewed.Rows.Select(row => new Row(
                row.Number, row.Document, row.Name, row.Role, row.Outcome, row.Problems))]));
    }
}
