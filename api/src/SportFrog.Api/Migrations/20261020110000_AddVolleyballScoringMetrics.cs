using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Flags volleyball's <c>point</c>, <c>ace</c> and the new
/// <c>block_point</c> metrics as score-affecting, and splits <c>block</c>
/// into that scoring half and a <c>block_touch</c> half that stays a pure
/// statistic. Also flags <c>error</c> as the mirror of an own goal: worth a
/// point to the opponent, never to the player who committed it. Lets a set's
/// result be finished from the events already recorded live instead of
/// typed in by hand — see
/// <see cref="SportFrog.Api.Features.Matches.RecordResult"/>.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020110000_AddVolleyballScoringMetrics")]
public sealed class AddVolleyballScoringMetrics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020110000_AddVolleyballScoringMetrics.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020110000_AddVolleyballScoringMetrics.Down.sql"));
}
