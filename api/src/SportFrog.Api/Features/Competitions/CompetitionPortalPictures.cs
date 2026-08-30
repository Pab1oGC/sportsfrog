using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Competitions;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Turns the pictures inside a competition's <see cref="PublicSettings"/> —
/// the banner, and every sponsor's mark — from whatever a caller sent into
/// what gets kept: a fresh data URL becomes a stored key, and a key already
/// on file passes through untouched. Shared between create and update
/// because both write the same shape, and update alone also needs to notice
/// which keys a fresh save stopped pointing at.
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

        List<SponsorLink>? sponsors = null;

        if (requested.Sponsors is { Count: > 0 })
        {
            sponsors = new List<SponsorLink>(requested.Sponsors.Count);

            foreach (var sponsor in requested.Sponsors)
            {
                var logo = sponsor.LogoKey;

                if (!PortalPicture.IsStoredKey(logo))
                {
                    if (await pictures.StoreAsync("competition-sponsors", logo, cancellationToken)
                        is not { } stored)
                    {
                        return null;
                    }

                    logo = stored;
                }

                sponsors.Add(sponsor with { LogoKey = logo });
            }
        }

        return requested with { BannerKey = banner, Sponsors = sponsors };
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

        foreach (var sponsor in settings.Sponsors ?? [])
        {
            yield return sponsor.LogoKey;
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
