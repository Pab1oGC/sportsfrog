using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Categories;

/// <summary>
/// Whether anything has been entered into a category.
/// </summary>
/// <remarks>
/// The two references behave differently, and the difference is why this
/// check has to exist rather than being left to the database.
///
/// Teams point at a category with ON DELETE RESTRICT, so the database refuses
/// on its own and this only produces a better message. Matches point at it
/// with ON DELETE CASCADE, so the database would say nothing and take the
/// results with it — a delete that silently removes a played fixture is
/// exactly the kind of thing nobody notices until the standings are wrong.
///
/// Written as SQL because neither entity exists yet; both tables are already
/// in the schema. When those modules land this becomes a pair of navigations
/// and nothing outside this file changes.
/// </remarks>
internal sealed class CategoryUsage(SportFrogDbContext database)
{
    public async Task<bool> IsInUseAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var references = await database.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM (
                     SELECT 1 FROM teams   WHERE category_id = {categoryId}
                     UNION ALL
                     SELECT 1 FROM matches WHERE category_id = {categoryId}
                 ) AS uses
                 """)
            .SingleAsync(cancellationToken);

        return references > 0;
    }
}
