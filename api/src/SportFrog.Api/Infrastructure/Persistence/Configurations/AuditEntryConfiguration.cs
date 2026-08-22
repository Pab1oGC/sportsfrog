using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AuditEntry"/> onto the <c>audit_log</c> table.</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(x => x.Id);

        // Assigned by the database and never by the application: an
        // append-only log must not let its order be chosen.
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.UserEmail).HasColumnName("user_email");
        builder.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.Action).HasColumnName("action").IsRequired();
        builder.Property(x => x.Changes).HasColumnName("changes").HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.IpAddress).HasColumnName("ip_address");

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }
}
