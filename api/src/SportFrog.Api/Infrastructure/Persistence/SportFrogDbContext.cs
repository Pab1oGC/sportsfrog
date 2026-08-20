using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// The application's data access point.
///
/// The schema is defined by the migrations' SQL, not by this model: the
/// mapping below describes tables that already exist and never generates
/// them. Migrations are applied by the schema owner, out of band.
/// </summary>
public sealed class SportFrogDbContext(DbContextOptions<SportFrogDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Club> Clubs => Set<Club>();

    public DbSet<Athlete> Athletes => Set<Athlete>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Declared so the provider translates the CLR enum to the database
        // enum type instead of to text.
        modelBuilder.HasPostgresEnum<MembershipRole>("public", "membership_role");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
