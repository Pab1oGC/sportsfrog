using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AccreditationItem"/> onto <c>accreditation_items</c>.</summary>
internal sealed class AccreditationItemConfiguration : IEntityTypeConfiguration<AccreditationItem>
{
    public void Configure(EntityTypeBuilder<AccreditationItem> builder)
    {
        builder.ToTable("accreditation_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.Kind).HasColumnName("kind");
        builder.Property(x => x.Code).HasColumnName("code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.ColorHex).HasColumnName("color_hex");
        builder.Property(x => x.IconKey).HasColumnName("icon_key");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.CompetitionId, x.Kind, x.Code }).IsUnique();

        // No soft delete here, unlike most of the schema. A catalogue entry is
        // configuration, not a record of something that happened: nothing in
        // the system refers to it after a card is printed, because the batch
        // freezes what it printed rather than pointing back at these rows.
        builder.HasOne(x => x.Competition)
            .WithMany()
            .HasForeignKey(x => x.CompetitionId);
    }
}

/// <summary>Maps <see cref="AccreditationCategory"/> onto its table.</summary>
internal sealed class AccreditationCategoryConfiguration
    : IEntityTypeConfiguration<AccreditationCategory>
{
    public void Configure(EntityTypeBuilder<AccreditationCategory> builder)
    {
        builder.ToTable("accreditation_categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.Code).HasColumnName("code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.ColorHex).HasColumnName("color_hex").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.CompetitionId, x.Code }).IsUnique();

        builder.HasOne(x => x.Competition)
            .WithMany()
            .HasForeignKey(x => x.CompetitionId);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId);
    }
}

/// <summary>Maps the package a category carries.</summary>
internal sealed class AccreditationCategoryItemConfiguration
    : IEntityTypeConfiguration<AccreditationCategoryItem>
{
    public void Configure(EntityTypeBuilder<AccreditationCategoryItem> builder)
    {
        builder.ToTable("accreditation_category_items");

        builder.HasKey(x => new { x.CategoryId, x.ItemId });

        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.ItemId).HasColumnName("item_id");

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId);
    }
}

/// <summary>Maps <see cref="AthleteAccreditation"/> onto its table.</summary>
internal sealed class AthleteAccreditationConfiguration
    : IEntityTypeConfiguration<AthleteAccreditation>
{
    public void Configure(EntityTypeBuilder<AthleteAccreditation> builder)
    {
        builder.ToTable("athlete_accreditations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.AthleteId).HasColumnName("athlete_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // One card per person per competition. The database says so too; this
        // is here so a second insert fails in the change tracker rather than
        // at the end of a batch.
        builder.HasIndex(x => new { x.CompetitionId, x.AthleteId }).IsUnique();

        builder.HasOne(x => x.Athlete)
            .WithMany()
            .HasForeignKey(x => x.AthleteId);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId);

        builder.HasMany(x => x.Overrides)
            .WithOne(x => x.Accreditation)
            .HasForeignKey(x => x.AccreditationId);
    }
}

/// <summary>Maps one person's exceptions to their category's package.</summary>
internal sealed class AthleteAccreditationItemConfiguration
    : IEntityTypeConfiguration<AthleteAccreditationItem>
{
    public void Configure(EntityTypeBuilder<AthleteAccreditationItem> builder)
    {
        builder.ToTable("athlete_accreditation_items");

        builder.HasKey(x => new { x.AccreditationId, x.ItemId });

        builder.Property(x => x.OrgId).HasColumnName("org_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.AccreditationId).HasColumnName("accreditation_id");
        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.Granted).HasColumnName("granted");

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId);
    }
}
