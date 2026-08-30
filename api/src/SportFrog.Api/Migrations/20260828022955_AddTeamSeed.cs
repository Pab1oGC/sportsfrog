using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Adds <c>teams.seed</c>, the optional pot number a seeded group draw reads.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260828022955_AddTeamSeed")]
public sealed class AddTeamSeed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828022955_AddTeamSeed.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828022955_AddTeamSeed.Down.sql"));
}
