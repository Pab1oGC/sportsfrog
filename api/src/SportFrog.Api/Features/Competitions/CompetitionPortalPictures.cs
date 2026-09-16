using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Turns the pictures inside a competition's <see cref="PublicSettings"/> —
/// the banner, the logo, every sponsor's mark, and every gallery photo —
/// from whatever a caller sent into what gets kept: a fresh data URL becomes
/// a stored key, and a key already on file passes through untouched. Shared
/// between create and update because both write the same shape, and update
/// alone also needs to notice which keys a fresh save stopped pointing at.
/// </summary>
internal static class CompetitionPortalPictures
{
    /// <summary>
    /// Answers the settings with every picture uploaded and its data URL
    /// replaced by the key it was stored under — or null only when a
    /// picture in the request did not decode as an image at all, which the
    /// caller reports rather than half-saving.
    /// </summary>
    public static async Task<PublicSettings?> ResolveAsync(
        PublicSettings requested, PortalPicture pictures, CancellationToken cancellationToken)
    {
        var banner = requested.BannerKey;

        if (banner is not null && !PortalPicture.IsStoredKey(banner))
        {
            if (await pictures.StoreAsync("competition-banners", banner, cancellationToken) is not { } stored)
            {
                return null;
            }

            banner = stored;
        }

        var logo = requested.LogoKey;

        if (logo is not null && !PortalPicture.IsStoredKey(logo))
        {
            if (await pictures.StoreAsync("competition-logos", logo, cancellationToken) is not { } stored)
            {
                return null;
            }

            logo = stored;
        }

        List<SponsorLink>? sponsors = null;

        if (requested.Sponsors is { Count: > 0 })
        {
            sponsors = new List<SponsorLink>(requested.Sponsors.Count);

            foreach (var sponsor in requested.Sponsors)
            {
                var sponsorLogo = sponsor.LogoKey;

                if (!PortalPicture.IsStoredKey(sponsorLogo))
                {
                    if (await pictures.StoreAsync("competition-sponsors", sponsorLogo, cancellationToken)
                        is not { } stored)
                    {
                        return null;
                    }

                    sponsorLogo = stored;
                }

                sponsors.Add(sponsor with { LogoKey = sponsorLogo });
            }
        }

        List<GalleryPhoto>? gallery = null;

        if (requested.Gallery is { Count: > 0 })
        {
            gallery = new List<GalleryPhoto>(requested.Gallery.Count);

            foreach (var photo in requested.Gallery)
            {
                var photoKey = photo.Key;

                if (!PortalPicture.IsStoredKey(photoKey))
                {
                    if (await pictures.StoreAsync("competition-gallery", photoKey, cancellationToken)
                        is not { } stored)
                    {
                        return null;
                    }

                    photoKey = stored;
                }

                gallery.Add(photo with { Key = photoKey });
            }
        }

        return requested with { BannerKey = banner, LogoKey = logo, Sponsors = sponsors, Gallery = gallery };
    }

    /// <summary>Every stored key a public-settings block references.</summary>
    private static IEnumerable<string> Keys(PublicSettings? settings)
    {
        if (settings is null)
        {
            yield break;
        }

        if (settings.BannerKey is { } banner)
        {
            yield return banner;
        }

        if (settings.LogoKey is { } logo)
        {
            yield return logo;
        }

        foreach (var sponsor in settings.Sponsors ?? [])
        {
            yield return sponsor.LogoKey;
        }

        foreach (var photo in settings.Gallery ?? [])
        {
            yield return photo.Key;
        }
    }

    /// <summary>
    /// Forgets whatever the previous settings kept a picture under that the
    /// new settings no longer reference — a banner that was replaced, a
    /// sponsor that was dropped. Nothing else in object storage knows a
    /// competition's settings changed, so this is the one place that does.
    /// </summary>
    public static async Task ForgetOrphanedAsync(
        PublicSettings? previous,
        PublicSettings? next,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        var kept = Keys(next).ToHashSet(StringComparer.Ordinal);

        foreach (var key in Keys(previous))
        {
            if (!kept.Contains(key))
            {
                await pictures.ForgetAsync(key, cancellationToken);
            }
        }
    }
}
