using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Draws one person's credential as it would look right now, without issuing
/// anything.
/// </summary>
/// <remarks>
/// Decreeing the structure removed the one thing a template designer used to
/// give an operator for free: a look at the card before committing four
/// hundred of them to paper. This is that look, rebuilt for a card that has
/// no designer to preview it in.
///
/// Deliberately live, not frozen. <see cref="IssueDocumentsJob"/> prints from
/// <see cref="CredentialSnapshot"/> so a reissued card matches what was
/// actually handed out; this draws from the catalogue exactly as it stands
/// this second, because the entire point of a preview is to answer "what
/// would printing right now produce" — including a colour an operator just
/// changed and has not saved a batch against yet.
///
/// No serial, no QR, no visible id: none of those exist until a batch
/// actually claims them, and claiming a real sequential number just to throw
/// a preview away would be the one thing <see cref="CredentialNumbering"/>
/// exists to prevent — a gap in a sequence nobody can account for.
/// </remarks>
public static class PreviewCredential
{
    private const string PlaceholderVisibleId = "VISTA PREVIA";

    public static IEndpointRouteBuilder MapPreviewCredential(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(
                "/competitions/{competitionId:guid}/accreditation/athletes/{athleteId:guid}/preview.png",
                HandleAsync)
            .RequireRole(MembershipRole.Viewer)
            .WithName("PreviewCredential")
            .WithSummary("Renders what this person's credential would look like right now.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid competitionId,
        Guid athleteId,
        SportFrogDbContext database,
        ObjectStore store,
        AthletePhoto photos,
        AccreditationResolver resolver,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var organizationId = organization.RequireOrganizationId();

        var subject = await database.RosterEntries
            .AsNoTracking()
            .Where(entry => entry.WithdrawnAt == null)
            .Where(entry => entry.AthleteId == athleteId)
            .Where(entry => entry.Team!.Category!.CompetitionId == competitionId)
            .Select(entry => new
            {
                entry.Athlete!.FirstName,
                entry.Athlete.LastName,
                entry.Athlete.PhotoKey,
                entry.Athlete.DocumentId,
                ClubName = entry.Team!.Club!.Name,
                ClubLogoKey = entry.Team.Club.LogoUrl,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject is null)
        {
            return Results.NotFound();
        }

        var accreditation = await database.AthleteAccreditations
            .AsNoTracking()
            .Where(candidate => candidate.CompetitionId == competitionId && candidate.AthleteId == athleteId)
            .Select(candidate => new { candidate.Id, candidate.CategoryId })
            .SingleOrDefaultAsync(cancellationToken);

        if (accreditation is null)
        {
            return Results.Problem(
                detail: "Esta persona todavía no tiene una categoría de acreditación asignada, así que "
                        + "no hay nada que previsualizar.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var category = await database.AccreditationCategories
            .AsNoTracking()
            .Where(candidate => candidate.Id == accreditation.CategoryId)
            .Select(candidate => new { candidate.Code, candidate.Name, candidate.ColorHex })
            .SingleAsync(cancellationToken);

        var resolved = await resolver.ResolveAsync(accreditation.Id, cancellationToken);

        var competition = await database.Competitions
            .AsNoTracking()
            .Where(candidate => candidate.Id == competitionId)
            .Select(candidate => new { candidate.Name, candidate.Season, candidate.Settings, candidate.CredentialDesignId })
            .SingleAsync(cancellationToken);

        var organizationName = await database.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Id == organizationId)
            .Select(candidate => candidate.Name)
            .SingleAsync(cancellationToken);

        // Live rather than frozen, as the rest of this preview is: it shows what
        // printing now would produce, including a design an operator just saved.
        var design = await CredentialDesignChoice.LoadForAsync(
            competition.CredentialDesignId, database, cancellationToken);

        var resolution = CredentialDesignResolver.Resolve(
            competition.Settings.Public, CredentialDesignChoice.ToValues(design));

        var context = new CredentialContext(
            organizationName,
            competition.Name,
            competition.Season,
            resolution.LogoKey,
            resolution.AccentColorHex,
            resolution.LegalText,
            resolution.BackgroundKey);

        var print = new CredentialPrint(
            new CredentialSubject(
                $"{subject.FirstName} {subject.LastName}", subject.PhotoKey, subject.ClubName, subject.ClubLogoKey, subject.DocumentId),
            context,
            category.Code,
            category.Name,
            category.ColorHex,
            PlaceholderVisibleId,
            VerifyUrl: string.Empty,
            Grants: [.. resolved.Select(item =>
                new CredentialGrant(item.ItemId, item.Kind, item.Code, item.Name, item.ColorHex, item.IconKey))],
            IssuedAt: DateTimeOffset.UtcNow);

        var assets = new CredentialAssets(
            await photos.BytesAsync(organizationId, subject.PhotoKey, cancellationToken),
            await ArtworkAsync(store, organizationId, subject.ClubLogoKey, cancellationToken),
            await ArtworkAsync(store, organizationId, context.CompetitionLogoKey, cancellationToken),
            Qr: null,
            Background: CredentialWatermark.Fade(
                await ArtworkAsync(store, organizationId, context.BackgroundKey, cancellationToken)));

        var image = CredentialRenderer.BuildDocument(print, assets).GenerateImages().First();

        return Results.File(image, "image/png");
    }

    private static async Task<byte[]?> ArtworkAsync(
        ObjectStore store, Guid organizationId, string? key, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(key) ? null : await store.ReadAsync(organizationId, key, cancellationToken);
}
