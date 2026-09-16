using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Gives a classification-stage performance a mat and a turn: which venue
/// space, which day, and where in that day's running order — not a clock
/// time, which routines barely a minute apart would never actually keep to.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260908120000_AddPerformanceRunningOrder")]
public sealed class AddPerformanceRunningOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260908120000_AddPerformanceRunningOrder.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260908120000_AddPerformanceRunningOrder.Down.sql"));
}
