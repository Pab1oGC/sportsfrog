using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DocumentRequest"/> onto <c>document_requests</c>.</summary>
internal sealed class DocumentRequestConfiguration : IEntityTypeConfiguration<DocumentRequest>
{
    public void Configure(EntityTypeBuilder<DocumentRequest> builder)
    {
        builder.ToTable("document_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.CertificateType).HasColumnName("certificate_type");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidTo).HasColumnName("valid_to");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.CreatedAt });

        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
    }
}
