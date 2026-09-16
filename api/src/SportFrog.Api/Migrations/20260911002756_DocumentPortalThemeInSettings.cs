using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment: brings the
/// drifted schema doc current and adds the <c>public.theme</c> block. No
/// column change — the settings are jsonb.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260911002756_DocumentPortalThemeInSettings")]
public sealed class DocumentPortalThemeInSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911002756_DocumentPortalThemeInSettings.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911002756_DocumentPortalThemeInSettings.Down.sql"));
}
