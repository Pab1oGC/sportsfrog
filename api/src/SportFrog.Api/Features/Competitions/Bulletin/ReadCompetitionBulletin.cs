using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Competitions.Bulletin;

/// <summary>
/// Hands out a competition's bulletin — its categories, their rules and the
/// organizer's own prose — as a PDF or as an editable Word document.
/// </summary>
/// <remarks>
/// Generated on request rather than queued: unlike a batch of four hundred
/// credentials, this is one document, and composing it is cheap enough that
/// the request that asks for it can simply wait for the answer — see
/// <see cref="SportFrog.Api.Features.Documents.RequestDocumentBatch"/>'s own
/// remarks for why that one, unlike this, has to be a background job instead.
/// </remarks>
public static class ReadCompetitionBulletin
{
    public static IEndpointRouteBuilder MapReadCompetitionBulletin(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/competitions/{competitionId:guid}/bulletin.pdf", HandlePdfAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCompetitionBulletin) + "Pdf")
            .WithSummary("Renders a competition's bulletin as a PDF.");

        routes.MapGet("/competitions/{competitionId:guid}/bulletin.docx", HandleWordAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName(nameof(ReadCompetitionBulletin) + "Word")
            .WithSummary("Renders a competition's bulletin as an editable Word document.");

        return routes;
    }

    private static async Task<IResult> HandlePdfAsync(
        Guid competitionId, SportFrogDbContext database, ObjectStore store, CancellationToken cancellationToken)
    {
        var data = await CompetitionBulletinData.ReadAsync(database, store, competitionId, cancellationToken);

        return data is null
            ? Results.NotFound()
            : Results.File(CompetitionBulletinPdf.Render(data), "application/pdf", FileName(data, "pdf"));
    }

    private static async Task<IResult> HandleWordAsync(
        Guid competitionId, SportFrogDbContext database, ObjectStore store, CancellationToken cancellationToken)
    {
        var data = await CompetitionBulletinData.ReadAsync(database, store, competitionId, cancellationToken);

        return data is null
            ? Results.NotFound()
            : Results.File(
                CompetitionBulletinWord.Render(data),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName(data, "docx"));
    }

    private static string FileName(BulletinData data, string extension)
    {
        var readable = string.Concat(data.CompetitionName
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();

        return $"convocatoria-{(readable.Length > 0 ? readable : "competencia")}.{extension}";
    }
}
