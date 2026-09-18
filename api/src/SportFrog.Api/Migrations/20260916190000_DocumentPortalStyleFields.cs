using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment: extends
/// <c>public.theme</c> with <c>density</c>, <c>decoration</c> and
/// <c>hero_layout</c>. No column change — the settings are jsonb.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260916190000_DocumentPortalStyleFields")]
public sealed class DocumentPortalStyleFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260916190000_DocumentPortalStyleFields.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260916190000_DocumentPortalStyleFields.Down.sql"));
}
