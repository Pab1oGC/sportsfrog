using System.Reflection;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// RF-02 — every mapped entity must produce a SELECT that the real schema
/// actually accepts. A column name or type drifting out of sync with the
/// migrations' SQL fails here, not the first time it's hit in production.
/// </summary>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class EntityMappingTests(SportFrogDatabaseFixture fixture)
{
    /// <summary>
    /// Every CLR type EF has mapped, read from the model's own metadata. No
    /// live connection needed: model building doesn't touch the database, so
    /// this list stays correct as entities are added without editing this
    /// test.
    /// </summary>
    public static IEnumerable<object[]> MappedEntityTypes()
    {
        var options = new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql("Host=localhost;Database=model-metadata-only;Username=x;Password=x")
            .Options;

        using var context = new SportFrogDbContext(options);

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            yield return [entityType.ClrType];
        }
    }

    // DbContext only exposes Set<TEntity>() generically; there's no
    // non-generic Set(Type) overload, so reaching an arbitrary mapped type at
    // runtime means invoking the generic method through reflection.
    private static readonly MethodInfo SetMethod = typeof(DbContext)
        .GetMethods()
        .Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

    [Theory]
    [MemberData(nameof(MappedEntityTypes))]
    public async Task Set_MatchesTheRealSchema(Type entityClrType)
    {
        await using var context = fixture.CreateAppContext();

        var dbSet = (IQueryable)SetMethod.MakeGenericMethod(entityClrType).Invoke(context, null)!;

        // Take(0) still forces EF to build and send the full SELECT for
        // every mapped column: a name or type mismatch fails right here,
        // as a real error from Postgres, before any row is read.
        var query = dbSet.Cast<object>().Take(0);

        var fetching = async () => await query.ToListAsync();

        await fetching.Should().NotThrowAsync();
    }
}
