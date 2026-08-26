using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// A public directory of published competitions, across every organization.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260824235845_AddPublicCompetitionDirectory")]
public sealed class AddPublicCompetitionDirectory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824235845_AddPublicCompetitionDirectory.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824235845_AddPublicCompetitionDirectory.Down.sql"));
}
