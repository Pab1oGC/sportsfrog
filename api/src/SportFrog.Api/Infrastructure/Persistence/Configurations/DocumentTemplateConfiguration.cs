using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// How a layout is written into and read out of jsonb.
/// </summary>
/// <remarks>
/// Shared by the template and by its archive of versions, because the two
/// hold the same document and a difference between them would mean a design
/// read back from the archive was not the design that was saved.
///
/// Unknown properties are refused in both directions, which the records
/// themselves declare. The cost of that strictness is a rule the archive
/// imposes on whoever edits those records: a property may be added and must
/// never be removed, or every version stored under it becomes unreadable and
/// the credentials issued from it can no longer be reprinted.
/// </remarks>
internal static class LayoutStorage
{
    public static readonly JsonSerializerOptions Format = new()
    {
        // Snake case, so the column reads the way the schema comment says it
        // does. The layout is documented in the database itself, and psql has
        // to be a usable way to look at one.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

        // An absent property and one set to null say the same thing about a
        // field that has no colour, and the shorter one is easier to read.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Tells the change tracker how to tell one layout from another.
    /// </summary>
    /// <remarks>
    /// Required, like every other jsonb column here. Without it the tracker
    /// compares the converted value by reference, finds that a record holding
    /// a list is never the object it snapshotted, and rewrites the column on
    /// every save — so the audit log would report somebody redesigning a
    /// credential each time the template was so much as renamed.
    /// </remarks>
    public static readonly ValueComparer<TemplateLayout> Comparer = new(
        (left, right) =>
            JsonSerializer.Serialize(left, Format) == JsonSerializer.Serialize(right, Format),
        layout => JsonSerializer.Serialize(layout, Format).GetHashCode(),
        layout => JsonSerializer.Deserialize<TemplateLayout>(
            JsonSerializer.Serialize(layout, Format), Format)!);

    public static PropertyBuilder<TemplateLayout> AsLayout(this PropertyBuilder<TemplateLayout> builder) =>
        builder
            .HasColumnName("layout")
            .HasColumnType("jsonb")
            .HasConversion(
                layout => JsonSerializer.Serialize(layout, Format),
                stored => JsonSerializer.Deserialize<TemplateLayout>(stored, Format)!,
                Comparer);
}

/// <summary>Maps <see cref="DocumentTemplate"/> onto <c>document_templates</c>.</summary>
internal sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("document_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.PageSize).HasColumnName("page_size").IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.Property(x => x.Layout).AsLayout();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.OrgId, x.Kind }).HasFilter("deleted_at IS NULL");

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}

/// <summary>Maps <see cref="DocumentTemplateVersion"/> onto its archive table.</summary>
internal sealed class DocumentTemplateVersionConfiguration
    : IEntityTypeConfiguration<DocumentTemplateVersion>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateVersion> builder)
    {
        builder.ToTable("document_template_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.PageSize).HasColumnName("page_size").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder.Property(x => x.Layout).AsLayout();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        builder.HasIndex(x => new { x.TemplateId, x.Version }).IsUnique();

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId);

        // Deliberately not filtered by the template's soft delete, though the
        // obvious reading of "the template is gone" says it should be.
        //
        // Retiring a design means stop offering it, not undo it — that is the
        // whole reason the deletion is logical. A credential issued last
        // season under a design nobody uses any more still has to be
        // reprintable, and filtering these rows away with their template would
        // have made retiring a template quietly destroy the ability to reissue
        // everything printed from it.
    }
}
