using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Poomsae's catalog row — safe only now that the domain knows a fourth
/// score mode: <c>judged</c>. Seeding it before
/// <see cref="SportFrog.Domain.Rules.ScoreMode.Judged"/> and its
/// <c>IMatchOutcomeRules</c>/<c>IRulesetShapeRules</c>/<c>IResultShapeRules</c>
/// implementations existed would have made
/// <c>SportConfiguration.ScoreModeConverter</c> silently read this row back
/// as <c>cumulative</c> — see that converter's history for why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260906004617_SeedPoomsaeCatalog")]
public sealed class SeedPoomsaeCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906004617_SeedPoomsaeCatalog.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906004617_SeedPoomsaeCatalog.Down.sql"));
}
