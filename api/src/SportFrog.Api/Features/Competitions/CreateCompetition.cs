using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Features.Accreditation;
using SportFrog.Api.Features.Documents;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Sets up a competition (RF-06).
/// </summary>
public static class CreateCompetition
{
    public sealed record Response(Guid Id, string SportCode);

    public static IEndpointRouteBuilder MapCreateCompetition(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions", HandleAsync)
            // An administrator's decision, like the ruleset it binds. Running
            // the competition afterwards — recording results, closing matches
            // — is the operator's work and is gated separately.
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(CreateCompetition))
            .WithSummary("Sets up a competition.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        CompetitionContract contract,
        SportFrogDbContext database,
        OrganizationContext organization,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        // Loaded rather than merely checked for existence: its sport is what
        // fills the competition's own sport column, so the row is needed
        // either way and asking a validator to confirm it first would be
        // paying for the same lookup twice.
        var ruleset = await database.Rulesets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == contract.RulesetId, cancellationToken);

        if (ruleset is null)
        {
            // Not found rather than forbidden, and not distinguishable from a
            // ruleset belonging to another organization: the isolation policy
            // never returned it, so there is nothing here that could tell
            // them apart.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["RulesetId"] = ["No ruleset of this organization has that identifier."],
            });
        }

        if (await CredentialDesignChoice.RefuseUnknownAsync(
                contract.CredentialDesignId, database, cancellationToken) is { } unknownDesign)
        {
            return unknownDesign;
        }

        var slug = Slug.Normalize(contract.Slug);

        if (await database.Competitions.AnyAsync(
                competition => competition.Slug == slug, cancellationToken))
        {
            return CompetitionUniqueness.SlugTaken();
        }

        if (await CompetitionUniqueness.IsNameTakenAsync(
                database, contract.Name, except: null, cancellationToken))
        {
            return CompetitionUniqueness.NameTaken();
        }

        var settings = contract.Settings;

        if (settings?.Public is { } requestedPublic)
        {
            if (await CompetitionPortalPictures.ResolveAsync(requestedPublic, pictures, cancellationToken)
                is not { } resolvedPublic)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Settings.Public"] = ["Alguna imagen del portal público no se pudo leer."],
                });
            }

            settings = settings with { Public = resolvedPublic };
        }

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),

            // Taken from the ruleset, never from the request. The two cannot
            // disagree because only one of them was ever asked.
            SportCode = ruleset.SportCode,
            RulesetId = ruleset.Id,

            Name = contract.Name.Trim(),
            Slug = slug,
            Season = contract.Season.Trim(),
            Format = contract.Format,
            CaptureLevel = WireEnum.Parse<CaptureLevel>(contract.CaptureLevel),

            // Every competition begins in draft. Nothing is published and
            // nothing is played until someone moves it on.
            Status = CompetitionState.Draft,

            StartsOn = contract.StartsOn,
            EndsOn = contract.EndsOn,
            IsPublic = false,
            Settings = settings ?? new CompetitionSettings(),
            CredentialDesignId = contract.CredentialDesignId,
        };

        database.Competitions.Add(competition);

        // Written in the same save as the competition itself, so a competition
        // is never left behind without the catalogue it was created with.
        if (StartingCatalog.HasFor(competition.SportCode))
        {
            var catalog = StartingCatalog.Build(competition, DateTimeOffset.UtcNow);

            database.AccreditationItems.AddRange(catalog.Items);
            database.AccreditationCategories.AddRange(catalog.Categories);
            database.AccreditationCategoryItems.AddRange(catalog.Links);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (CompetitionUniqueness.ConflictFor(exception) is not null)
        {
            // Two requests claiming the same address or name at once. The
            // checks above answer the ordinary case; only the index sees this.
            return CompetitionUniqueness.ConflictFor(exception)!;
        }

        return Results.Created(
            $"/competitions/{competition.Id}",
            new Response(competition.Id, competition.SportCode));
    }
}
