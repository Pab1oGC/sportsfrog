using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment with the
/// event's own photo gallery (<c>show_gallery</c>, <c>gallery</c>) and adds
/// "gallery" to the documented <c>section_order</c> keys. No column change —
/// the settings are jsonb.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260911200231_DocumentPortalGallerySection")]
public sealed class DocumentPortalGallerySection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911200231_DocumentPortalGallerySection.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911200231_DocumentPortalGallerySection.Down.sql"));
}
