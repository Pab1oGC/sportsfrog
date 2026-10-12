using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Documents;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Which credential design a competition prints from, and checking that a
/// choice names one the organization actually has.
/// </summary>
/// <remarks>
/// Asked of the database rather than of the contract: which designs exist is
/// a fact the isolation policy answers, so a design of another organization
/// reads as not found, the same way a ruleset does. An unset choice is valid
/// and means the organization's default.
/// </remarks>
internal static class CredentialDesignChoice
{
    /// <summary>A refusal naming the field, or null when the choice is acceptable.</summary>
    public static async Task<IResult?> RefuseUnknownAsync(
        Guid? designId, SportFrogDbContext database, CancellationToken cancellationToken)
    {
        if (designId is not { } id)
        {
            return null;
        }

        var exists = await database.CredentialDesigns
            .AsNoTracking()
            .AnyAsync(design => design.Id == id, cancellationToken);

        return exists
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["CredentialDesignId"] = ["No credential design of this organization has that identifier."],
            });
    }

    /// <summary>
    /// The design a competition prints from: the one it chose, or the
    /// organization's default when it chose none. Null when neither exists,
    /// which prints the credential with the decree's defaults.
    /// </summary>
    public static async Task<CredentialDesign?> LoadForAsync(
        Guid? chosenId, SportFrogDbContext database, CancellationToken cancellationToken) =>
        chosenId is { } id
            ? await database.CredentialDesigns
                .AsNoTracking()
                .SingleOrDefaultAsync(design => design.Id == id, cancellationToken)
            // First rather than single: two defaults can only come from two
            // writes racing, and a batch should still print from one of them
            // rather than fail.
            : await database.CredentialDesigns
                .AsNoTracking()
                .Where(design => design.IsDefault)
                .OrderBy(design => design.Id)
                .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The stored values of a design, in the form the domain resolves.</summary>
    public static CredentialDesignValues? ToValues(CredentialDesign? design) =>
        design is null
            ? null
            : new CredentialDesignValues(design.LegalText, design.BackgroundKey, design.AccentColorHex, design.LogoKey);
}
