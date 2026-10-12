using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Keeps an organization's default credential design unique.
/// </summary>
/// <remarks>
/// Kept by the application rather than by a unique index, the same choice
/// <see cref="TemplateWriter"/> makes for document templates: an index would
/// also forbid an organization from having no default, which is the state it
/// is in before it has designed anything.
///
/// The first design an organization creates becomes its default without being
/// asked. Otherwise a competition that never picked one would print plain,
/// which is never what anybody meant by "the design we made".
/// </remarks>
internal static class CredentialDesignDefaults
{
    /// <summary>
    /// Whether a design being saved should be the default, given the current
    /// state. Only the first design of an organization is made default
    /// automatically; after that the choice is the caller's.
    /// </summary>
    public static async Task<bool> ShouldBeDefaultAsync(
        SportFrogDbContext database, bool requested, CancellationToken cancellationToken) =>
        requested || !await database.CredentialDesigns.AnyAsync(cancellationToken);

    /// <summary>
    /// Stops every other design of the organization being the default, when
    /// this one is. Staged, so the caller's save writes both changes together.
    /// </summary>
    public static async Task ClearOtherDefaultsAsync(
        SportFrogDbContext database, CredentialDesign design, CancellationToken cancellationToken)
    {
        if (!design.IsDefault)
        {
            return;
        }

        var others = await database.CredentialDesigns
            .Where(candidate => candidate.Id != design.Id && candidate.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }
}
