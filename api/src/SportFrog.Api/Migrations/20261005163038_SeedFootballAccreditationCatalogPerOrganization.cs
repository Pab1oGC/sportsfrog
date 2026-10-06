using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// TODO: describe in one line what this migration changes and why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261005163038_SeedFootballAccreditationCatalogPerOrganization")]
public sealed class SeedFootballAccreditationCatalogPerOrganization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261005163038_SeedFootballAccreditationCatalogPerOrganization.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261005163038_SeedFootballAccreditationCatalogPerOrganization.Down.sql"));
}
