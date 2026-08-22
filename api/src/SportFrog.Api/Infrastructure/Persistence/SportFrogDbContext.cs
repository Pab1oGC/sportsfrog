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

    /// <summary>
    /// The shared sports catalog. Read-only for the application: its content
    /// is seeded and amended by migration, because supporting a sport means
    /// knowing how to score it and not merely having a name for it.
    /// </summary>
    public DbSet<Sport> Sports => Set<Sport>();

    public DbSet<SportMetric> SportMetrics => Set<SportMetric>();

    public DbSet<Ruleset> Rulesets => Set<Ruleset>();

    public DbSet<Competition> Competitions => Set<Competition>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<VenueSpace> VenueSpaces => Set<VenueSpace>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Declared so the provider translates the CLR enum to the database
        // enum type instead of to text.
        modelBuilder.HasPostgresEnum<MembershipRole>("public", "membership_role");
        modelBuilder.HasPostgresEnum<CompetitionState>("public", "competition_state");
        modelBuilder.HasPostgresEnum<CaptureLevel>("public", "capture_level");
        modelBuilder.HasPostgresEnum<MatchState>("public", "match_state");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
