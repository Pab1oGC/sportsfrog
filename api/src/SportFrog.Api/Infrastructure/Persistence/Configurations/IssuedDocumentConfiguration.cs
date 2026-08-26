using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="IssuedDocument"/> onto <c>issued_documents</c>.</summary>
internal sealed class IssuedDocumentConfiguration : IEntityTypeConfiguration<IssuedDocument>
{
    public void Configure(EntityTypeBuilder<IssuedDocument> builder)
    {
        builder.ToTable("issued_documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.TemplateVersion).HasColumnName("template_version");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").IsRequired();
        builder.Property(x => x.CertificateType).HasColumnName("certificate_type");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidTo).HasColumnName("valid_to");
        builder.Property(x => x.PdfUrl).HasColumnName("pdf_url");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.IssuedBy).HasColumnName("issued_by");
        builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");

        builder.Property(x => x.IssuedAt)
            .HasColumnName("issued_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.SerialNumber }).IsUnique();
        builder.HasIndex(x => new { x.CompetitionId, x.Kind, x.Status });

        builder.HasOne(x => x.Athlete).WithMany().HasForeignKey(x => x.AthleteId);
        builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);

        // No query filter. There is no soft delete here and there is not going
        // to be one: a revoked credential is not a hidden row, it is a row
        // that answers "no" — which is exactly what public verification has to
        // be able to say (RF-45).
    }
}

/// <summary>Maps <see cref="DocumentBatch"/> onto <c>document_batches</c>.</summary>
internal sealed class DocumentBatchConfiguration : IEntityTypeConfiguration<DocumentBatch>
{
    private static readonly JsonSerializerOptions StorageFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public void Configure(EntityTypeBuilder<DocumentBatch> builder)
    {
        builder.ToTable("document_batches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.TemplateVersion).HasColumnName("template_version");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.CertificateType).HasColumnName("certificate_type");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidTo).HasColumnName("valid_to");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Total).HasColumnName("total");
        builder.Property(x => x.Issued).HasColumnName("issued");
        builder.Property(x => x.Skipped).HasColumnName("skipped");
        builder.Property(x => x.SheetKey).HasColumnName("sheet_key");
        builder.Property(x => x.Failure).HasColumnName("failure");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at");

        builder.Property(x => x.Problems)
            .HasColumnName("problems")
            .HasColumnType("jsonb")
            .HasConversion(
                problems => JsonSerializer.Serialize(problems, StorageFormat),
                stored => JsonSerializer.Deserialize<List<DocumentProblem>>(stored, StorageFormat)!,
                ProblemsComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.CreatedAt });

        builder.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId);
    }

    /// <summary>
    /// Required, like every other jsonb column here: without it the change
    /// tracker compares the converted value by reference and rewrites the
    /// column on every save, so the audit log would report a finished batch
    /// changing each time anything else touched the row.
    /// </summary>
    private static readonly ValueComparer<IReadOnlyList<DocumentProblem>> ProblemsComparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, StorageFormat)
            == JsonSerializer.Serialize(right, StorageFormat),
        problems => JsonSerializer.Serialize(problems, StorageFormat).GetHashCode(),
        problems => JsonSerializer.Deserialize<List<DocumentProblem>>(
            JsonSerializer.Serialize(problems, StorageFormat), StorageFormat)!);
}
