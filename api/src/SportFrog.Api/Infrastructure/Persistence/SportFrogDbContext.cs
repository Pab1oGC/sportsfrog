using Microsoft.EntityFrameworkCore;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// Minimal context, with no <c>DbSet</c>: the schema is defined by the
/// migrations' SQL, not by the model. Exists so the EF Core tools can
/// discover and apply those migrations.
/// </summary>
public sealed class SportFrogDbContext(DbContextOptions<SportFrogDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
