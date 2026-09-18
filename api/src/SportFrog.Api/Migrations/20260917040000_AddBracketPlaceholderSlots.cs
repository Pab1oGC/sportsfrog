using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Makes <c>matches.home_team_id</c>/<c>away_team_id</c> nullable and adds
/// <c>home_source_match_id</c>/<c>away_source_match_id</c>, so a knockout can
/// be drawn — and scheduled — all the way to the final before a single match
/// is played. See <see cref="SportFrog.Domain.Scheduling.Bracket.FullDraw"/>.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260917040000_AddBracketPlaceholderSlots")]
public sealed class AddBracketPlaceholderSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917040000_AddBracketPlaceholderSlots.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917040000_AddBracketPlaceholderSlots.Down.sql"));
}
