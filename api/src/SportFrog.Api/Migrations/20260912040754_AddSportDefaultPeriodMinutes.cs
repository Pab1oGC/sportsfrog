using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Gives each sport a standard clock length per period, so a reglamento form can prefill it.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260912040754_AddSportDefaultPeriodMinutes")]
public sealed class AddSportDefaultPeriodMinutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260912040754_AddSportDefaultPeriodMinutes.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260912040754_AddSportDefaultPeriodMinutes.Down.sql"));
}
