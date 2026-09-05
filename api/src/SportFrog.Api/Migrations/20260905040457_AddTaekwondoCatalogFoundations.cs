using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Schema and catalog groundwork for taekwondo: an individual-entrant flag on
/// the sports catalog, weight-class fields, an organization's club for
/// athletes with no delegation, and kyorugi's own catalog row.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260905040457_AddTaekwondoCatalogFoundations")]
public sealed class AddTaekwondoCatalogFoundations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260905040457_AddTaekwondoCatalogFoundations.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260905040457_AddTaekwondoCatalogFoundations.Down.sql"));
}
