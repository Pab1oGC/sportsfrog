using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Turns the pictures in a credential design's contract into what gets kept:
/// a new upload becomes a stored key, and a key already on file is kept only
/// if it belongs to this organization.
/// </summary>
/// <remarks>
/// The ownership check is the reason this is not simply
/// <see cref="PortalPicture.IsStoredKey"/>. That method only tells a key from
/// a data URL; it does not say whose key it is, and a design that accepted
/// another organization's key would print that organization's artwork.
/// </remarks>
internal static class CredentialDesignPictures
{
    public sealed record Resolved(string? BackgroundKey, string? LogoKey);

    /// <summary>
    /// The keys to store, or null when a picture was refused — whether it was
    /// not an image, or it was a key this organization does not own.
    /// </summary>
    public static async Task<(Resolved? Pictures, string? RefusedField)> ResolveAsync(
        CredentialDesignContract contract,
        PortalPicture pictures,
        OrganizationContext organization,
        CancellationToken cancellationToken)
    {
        var organizationId = organization.RequireOrganizationId();

        var background = await ResolveOneAsync(
            contract.BackgroundKey, "credential-backgrounds", organizationId, pictures, cancellationToken);

        if (background.Refused)
        {
            return (null, nameof(CredentialDesignContract.BackgroundKey));
        }

        var logo = await ResolveOneAsync(
            contract.LogoKey, "credential-logos", organizationId, pictures, cancellationToken);

        if (logo.Refused)
        {
            return (null, nameof(CredentialDesignContract.LogoKey));
        }

        return (new Resolved(background.Key, logo.Key), null);
    }

    private static async Task<(string? Key, bool Refused)> ResolveOneAsync(
        string? value,
        string category,
        Guid organizationId,
        PortalPicture pictures,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (null, false);
        }

        if (PortalPicture.IsStoredKey(value))
        {
            return StorageKeys.Belongs(organizationId, value) ? (value, false) : (null, true);
        }

        var stored = await pictures.StoreAsync(category, value, cancellationToken);

        return stored is null ? (null, true) : (stored, false);
    }
}
