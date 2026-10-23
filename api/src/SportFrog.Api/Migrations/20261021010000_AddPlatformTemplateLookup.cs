using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds document_template_org(), the platform organization's way to resolve
/// which organization a document_templates row belongs to regardless of
/// which one is current — what UpdateTemplate and DeleteTemplate need now
/// that authoring moved to the platform organization alone.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261021010000_AddPlatformTemplateLookup")]
public sealed class AddPlatformTemplateLookup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261021010000_AddPlatformTemplateLookup.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261021010000_AddPlatformTemplateLookup.Down.sql"));
}
