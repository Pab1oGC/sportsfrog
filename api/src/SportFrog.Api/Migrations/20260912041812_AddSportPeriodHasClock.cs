using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Separates "runs on a clock" from "played in sets" — Kyorugi is both.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260912041812_AddSportPeriodHasClock")]
public sealed class AddSportPeriodHasClock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260912041812_AddSportPeriodHasClock.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260912041812_AddSportPeriodHasClock.Down.sql"));
}
