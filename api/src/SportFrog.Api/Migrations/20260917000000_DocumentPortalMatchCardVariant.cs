using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment: extends
/// <c>public.theme</c> with <c>match_card_variant</c>. No column change —
/// the settings are jsonb.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260917000000_DocumentPortalMatchCardVariant")]
public sealed class DocumentPortalMatchCardVariant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917000000_DocumentPortalMatchCardVariant.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917000000_DocumentPortalMatchCardVariant.Down.sql"));
}
