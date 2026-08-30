using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// TODO: describe in one line what this migration changes and why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260828050653_AddMetricScoring")]
public sealed class AddMetricScoring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828050653_AddMetricScoring.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828050653_AddMetricScoring.Down.sql"));
}
