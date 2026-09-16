using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment with the
/// theme's focal point and the section order. No column change — the
/// settings are jsonb, and the moment shown on a cover is never stored.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260911192039_DocumentPortalMomentAndSections")]
public sealed class DocumentPortalMomentAndSections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911192039_DocumentPortalMomentAndSections.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260911192039_DocumentPortalMomentAndSections.Down.sql"));
}
