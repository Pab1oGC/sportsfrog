using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Bootstrap sports catalog: the five sports and their basic metrics.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260813120100_SeedCatalog")]
public sealed class SeedCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260813120100_SeedCatalog.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260813120100_SeedCatalog.Down.sql"));
}
