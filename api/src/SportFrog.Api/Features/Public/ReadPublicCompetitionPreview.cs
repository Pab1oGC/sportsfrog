using System.Net;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Public;

/// <summary>
/// A minimal, crawler-only stand-in for a competition's public page: a
/// title, a description and a picture, as the Open Graph and Twitter Card
/// tags a link-preview reads the instant a link is pasted — before anyone
/// has clicked it.
/// </summary>
/// <remarks>
/// A person never lands here. The single-page app has nothing in its own
/// unrendered <c>index.html</c> for a crawler to read — WhatsApp, Facebook
/// and the rest do not run its JavaScript — so a link to the real page would
/// otherwise unfurl as the site's bare name and nothing else. nginx alone
/// decides who reaches this: a request whose own user agent names one of
/// those crawlers is routed here instead of the app; every browser, and
/// every crawler nginx does not recognize, still reaches the single-page
/// app directly. The refresh tag below is only the fallback for the one way
/// that routing could still be wrong — this address reached by something
/// that does run JavaScript — and sends it straight on to the real page.
/// </remarks>
public static class ReadPublicCompetitionPreview
{
    public static IEndpointRouteBuilder MapReadPublicCompetitionPreview(this IEndpointRouteBuilder routes)
    {
        routes.MapGet($"{PublicRoutes.Prefix}/preview.html", HandleAsync)
            .AsPublicReading()
            .WithName(nameof(ReadPublicCompetitionPreview))
            .WithSummary("A crawler-only Open Graph / Twitter Card stand-in for a competition's public page.");

        return routes;
    }

    internal sealed record PreviewData(
        string Title, string Description, string? ImageUrl, string OrganizationSlug, string CompetitionSlug);

    private static async Task<IResult> HandleAsync(
        string organizationSlug,
        string competitionSlug,
        PublicCompetitionReader reader,
        ObjectStore store,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var page = await reader.ReadAsync(
            organizationSlug,
            competitionSlug,
            async (database, resolved) =>
            {
                var competition = await database.Competitions
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == resolved.CompetitionId)
                    .Select(candidate => new
                    {
                        candidate.Name,
                        candidate.Season,
                        candidate.Settings,
                        SportName = candidate.Sport!.Name,
                        OrganizationName = database.Organizations
                            .Where(org => org.Id == resolved.OrganizationId)
                            .Select(org => org.Name)
                            .First(),
                    })
                    .SingleAsync(cancellationToken);

                var shows = competition.Settings.Public;

                // The banner over the logo, the same preference the cover
                // itself gives an "image" hero — a wide picture reads better
                // as a link's own preview than a badge-shaped mark does.
                var imageKey = !string.IsNullOrEmpty(shows?.BannerKey) ? shows.BannerKey : shows?.LogoKey;

                var imageUrl = imageKey is { } key
                    ? await store.ReadLinkAsync(resolved.OrganizationId, key, cancellationToken)
                    : null;

                var description = !string.IsNullOrWhiteSpace(shows?.Description)
                    ? shows!.Description!
                    : $"{competition.OrganizationName} · {competition.SportName} · {competition.Season}";

                return new PreviewData(
                    $"{competition.Name} · {competition.OrganizationName}",
                    description,
                    imageUrl,
                    organizationSlug,
                    competitionSlug);
            },
            cancellationToken);

        if (page is null)
        {
            return Results.NotFound();
        }

        // Rebuilt from the request the visitor's own browser or crawler
        // actually made, not a configured base URL — this address has no
        // way to be wrong about which host or scheme it was reached
        // through, and a setting could drift out of step with either.
        var appUrl = $"{request.Scheme}://{request.Host}/public/{Uri.EscapeDataString(page.OrganizationSlug)}/{Uri.EscapeDataString(page.CompetitionSlug)}";

        return Results.Text(Render(page, appUrl), "text/html", System.Text.Encoding.UTF8);
    }

    internal static string Render(PreviewData data, string appUrl)
    {
        var title = Encode(data.Title);
        var description = Encode(Truncate(data.Description, 200));
        var url = Encode(appUrl);
        var image = data.ImageUrl is { } raw ? Encode(raw) : null;
        var cardType = image is null ? "summary" : "summary_large_image";

        var imageTags = image is null
            ? ""
            : $"""
               <meta property="og:image" content="{image}">
               <meta name="twitter:image" content="{image}">
               """;

        return $"""
            <!doctype html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <title>{title}</title>
            <meta name="description" content="{description}">
            <meta property="og:type" content="website">
            <meta property="og:title" content="{title}">
            <meta property="og:description" content="{description}">
            <meta property="og:url" content="{url}">
            {imageTags}
            <meta name="twitter:card" content="{cardType}">
            <meta name="twitter:title" content="{title}">
            <meta name="twitter:description" content="{description}">
            <meta http-equiv="refresh" content="0; url={url}">
            </head>
            <body></body>
            </html>
            """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    internal static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
}
