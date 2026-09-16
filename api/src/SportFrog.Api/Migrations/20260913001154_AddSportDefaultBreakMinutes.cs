using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Gives each sport a standard rest between periods, so a reglamento form can prefill it.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260913001154_AddSportDefaultBreakMinutes")]
public sealed class AddSportDefaultBreakMinutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260913001154_AddSportDefaultBreakMinutes.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260913001154_AddSportDefaultBreakMinutes.Down.sql"));
}
