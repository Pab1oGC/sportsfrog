using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// TODO: describe in one line what this migration changes and why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260909224020_SeedRealChampionshipsData")]
public sealed class SeedRealChampionshipsData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260909224020_SeedRealChampionshipsData.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260909224020_SeedRealChampionshipsData.Down.sql"));
}
