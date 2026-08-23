using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// The one place a design is written down.
/// </summary>
/// <remarks>
/// A template and its archive of versions are two rows saying the same thing,
/// which is a standing invitation to drift. Keeping the only writer here means
/// they cannot: a layout is never saved without the version that records it,
/// and the number on the template is never bumped without a row to match.
///
/// Whoever adds a way to edit a design later has one rule to follow — come
/// through here — and the comment exists so that rule is visible from the
/// place where it would be broken.
/// </remarks>
internal sealed class TemplateWriter(SportFrogDbContext database, OrganizationContext organization)
{
    /// <summary>
    /// Records the template's current layout as a new version.
    /// </summary>
    /// <remarks>
    /// Staged rather than saved: the caller decides when the transaction ends,
    /// and both rows go in together or neither does.
    /// </remarks>
    public void Record(DocumentTemplate template) =>
        database.Set<DocumentTemplateVersion>().Add(new DocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            OrgId = template.OrgId,
            TemplateId = template.Id,
            Version = template.Version,
            Layout = template.Layout,
            PageSize = template.PageSize,
            CreatedBy = organization.UserId,
        });

    /// <summary>
    /// Makes this template the one offered first, and stops the others being.
    /// </summary>
    /// <remarks>
    /// Kept by the application rather than by a unique index, deliberately.
    /// An index would also forbid an organization from having no default at
    /// all, which is exactly the state it is in before anybody has designed
    /// anything.
    /// </remarks>
    public async Task ClearOtherDefaultsAsync(
        DocumentTemplate template,
        CancellationToken cancellationToken)
    {
        if (!template.IsDefault)
        {
            return;
        }

        var others = await database.Set<DocumentTemplate>()
            .Where(candidate => candidate.Kind == template.Kind
                && candidate.Id != template.Id
                && candidate.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }
}
