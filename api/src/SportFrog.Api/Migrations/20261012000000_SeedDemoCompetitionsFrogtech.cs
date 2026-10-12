using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Loads three demonstration competitions, with their clubs, teams, athletes
/// and matches, into the platform organization.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261012000000_SeedDemoCompetitionsFrogtech")]
public sealed class SeedDemoCompetitionsFrogtech : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261012000000_SeedDemoCompetitionsFrogtech.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261012000000_SeedDemoCompetitionsFrogtech.Down.sql"));
}
