using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Adds <c>matches.penalty_home_score</c> and <c>penalty_away_score</c>, for shootouts.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260828200339_AddPenaltyShootout")]
public sealed class AddPenaltyShootout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828200339_AddPenaltyShootout.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828200339_AddPenaltyShootout.Down.sql"));
}
