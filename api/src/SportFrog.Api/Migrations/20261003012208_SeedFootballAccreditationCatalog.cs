using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Seeds a starting accreditation catalogue, from the Pan American Sports
/// Organization's accreditation code system, for every football competition.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261003012208_SeedFootballAccreditationCatalog")]
public sealed class SeedFootballAccreditationCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261003012208_SeedFootballAccreditationCatalog.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261003012208_SeedFootballAccreditationCatalog.Down.sql"));
}
