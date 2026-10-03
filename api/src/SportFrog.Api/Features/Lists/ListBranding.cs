using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Storage;

namespace SportFrog.Api.Features.Lists;

/// <summary>
/// The competition's mark and colour for whichever list is being exported —
/// the same pair <see cref="CompetitionBranding"/> already reads once for
/// the fixture, the bulletin, and both reports, so an exported list looks
/// like it belongs to the same competition as the convocatoria someone
/// already printed.
/// </summary>
/// <remarks>
/// A list does not carry its own competition id the way those four
/// documents' own handlers already do — it carries a <c>categoryId</c> or a
/// <c>teamId</c>, whichever its provider declared. This reads the same two
/// names every category/team-scoped provider's parameters already use
/// (<see cref="ListParameterKind.Category"/>, <see cref="ListParameterKind.Team"/>)
/// directly off the scope, rather than asking each provider to also resolve
/// and hand back a competition id: which list a row belongs to is the
/// provider's job, and branding is a presentation concern on top of that,
/// the same way <see cref="ReadLists"/> itself decides row caps and file
/// names without any provider's help.
///
/// A list with neither — <c>AthletesList</c>, scoped to the whole
/// organization — has no one competition to brand with, which is not a
/// failure: <see cref="CompetitionBranding.None"/> reads everywhere else as
/// "draw this the way every document always has".
/// </remarks>
internal static class ListBranding
{
    public static async Task<CompetitionBranding> ResolveAsync(
        ListScope scope, SportFrogDbContext database, ObjectStore store, CancellationToken cancellationToken)
    {
        if (scope.GetGuid("categoryId") is { } categoryId)
        {
            var competitionId = await database.Categories
                .AsNoTracking()
                .Where(category => category.Id == categoryId)
                .Select(category => (Guid?)category.CompetitionId)
                .SingleOrDefaultAsync(cancellationToken);

            return await ReadOrNoneAsync(database, store, competitionId, cancellationToken);
        }

        if (scope.GetGuid("teamId") is { } teamId)
        {
            var competitionId = await database.Teams
                .AsNoTracking()
                .Where(team => team.Id == teamId)
                .Select(team => (Guid?)team.Category!.CompetitionId)
                .SingleOrDefaultAsync(cancellationToken);

            return await ReadOrNoneAsync(database, store, competitionId, cancellationToken);
        }

        return CompetitionBranding.None;
    }

    private static Task<CompetitionBranding> ReadOrNoneAsync(
        SportFrogDbContext database, ObjectStore store, Guid? competitionId, CancellationToken cancellationToken) =>
        competitionId is { } id
            ? CompetitionBranding.ReadAsync(database, store, id, cancellationToken)
            : Task.FromResult(CompetitionBranding.None);
}
