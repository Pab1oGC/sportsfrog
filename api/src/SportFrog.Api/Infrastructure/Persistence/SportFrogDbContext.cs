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

    public DbSet<PlayerEvent> PlayerEvents => Set<PlayerEvent>();

    public DbSet<Performance> Performances => Set<Performance>();

    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    public DbSet<CredentialDesign> CredentialDesigns => Set<CredentialDesign>();

    public DbSet<DocumentTemplateVersion> DocumentTemplateVersions => Set<DocumentTemplateVersion>();

    public DbSet<IssuedDocument> IssuedDocuments => Set<IssuedDocument>();

    public DbSet<DocumentBatch> DocumentBatches => Set<DocumentBatch>();

    public DbSet<AccreditationItem> AccreditationItems => Set<AccreditationItem>();

    public DbSet<AccreditationCategory> AccreditationCategories => Set<AccreditationCategory>();

    public DbSet<AccreditationCategoryItem> AccreditationCategoryItems =>
        Set<AccreditationCategoryItem>();

    public DbSet<AthleteAccreditation> AthleteAccreditations => Set<AthleteAccreditation>();

    public DbSet<AthleteAccreditationItem> AthleteAccreditationItems =>
        Set<AthleteAccreditationItem>();

    public DbSet<PhotoImport> PhotoImports => Set<PhotoImport>();

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
        modelBuilder.HasPostgresEnum<PhotoImportState>("public", "photo_import_state");
        modelBuilder.HasPostgresEnum<DocumentKind>("public", "document_kind");
        modelBuilder.HasPostgresEnum<DocumentState>("public", "document_state");
        modelBuilder.HasPostgresEnum<DocumentBatchState>("public", "document_batch_state");
        modelBuilder.HasPostgresEnum<PerformanceStatus>("public", "performance_status");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
