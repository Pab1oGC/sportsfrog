using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Moves athlete photographs out of the database and into object storage.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260822193319_AthletePhotosToObjectStorage")]
public sealed class AthletePhotosToObjectStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260822193319_AthletePhotosToObjectStorage.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260822193319_AthletePhotosToObjectStorage.Down.sql"));
}
