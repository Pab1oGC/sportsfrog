using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// The catalogue a decreed credential prints from: zones, services, venues,
/// the discipline, and the accreditation categories that package them.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261002215033_AddAccreditationCatalog")]
public sealed class AddAccreditationCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261002215033_AddAccreditationCatalog.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261002215033_AddAccreditationCatalog.Down.sql"));
}
