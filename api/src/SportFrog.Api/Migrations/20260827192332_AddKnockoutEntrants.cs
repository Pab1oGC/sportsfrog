using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Adds <c>categories.knockout_entrants</c>, for byes once groups and a knockout mix.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260827192332_AddKnockoutEntrants")]
public sealed class AddKnockoutEntrants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260827192332_AddKnockoutEntrants.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260827192332_AddKnockoutEntrants.Down.sql"));
}
