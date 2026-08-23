using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PhotoImport"/> onto the <c>photo_imports</c> table.</summary>
internal sealed class PhotoImportConfiguration : IEntityTypeConfiguration<PhotoImport>
{
    /// <summary>
    /// Snake case, so the column reads the way its comment in the schema says
    /// it does and psql is a usable way to look at a batch that went wrong.
    /// </summary>
    private static readonly JsonSerializerOptions StorageFormat = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public void Configure(EntityTypeBuilder<PhotoImport> builder)
    {
        builder.ToTable("photo_imports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(x => x.ArchiveKey).HasColumnName("archive_key").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Total).HasColumnName("total");
        builder.Property(x => x.Matched).HasColumnName("matched");
        builder.Property(x => x.Failed).HasColumnName("failed");
        builder.Property(x => x.Failure).HasColumnName("failure");
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at");

        builder.Property(x => x.Results)
            .HasColumnName("results")
            .HasColumnType("jsonb")
            .HasConversion(
                results => JsonSerializer.Serialize(results, StorageFormat),
                stored => JsonSerializer.Deserialize<List<PhotoResult>>(stored, StorageFormat)!,
                ResultsComparer);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.CreatedAt });
    }

    /// <summary>
    /// Tells the change tracker how to tell one set of results from another.
    /// </summary>
    /// <remarks>
    /// Required for the same reason every other jsonb column here has one:
    /// without it the tracker compares the converted value by reference,
    /// finds a list is never the same object it snapshotted, and rewrites the
    /// column on every save — which for this table would mean the audit log
    /// reporting a change to a finished batch each time anything else touched
    /// it.
    /// </remarks>
    private static readonly ValueComparer<IReadOnlyList<PhotoResult>> ResultsComparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, StorageFormat)
            == JsonSerializer.Serialize(right, StorageFormat),
        results => JsonSerializer.Serialize(results, StorageFormat).GetHashCode(),
        results => JsonSerializer.Deserialize<List<PhotoResult>>(
            JsonSerializer.Serialize(results, StorageFormat), StorageFormat)!);
}
