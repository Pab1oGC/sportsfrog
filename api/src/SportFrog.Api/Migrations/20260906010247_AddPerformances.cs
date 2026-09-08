using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Poomsae's classification stage: N competitors, each producing one judged
/// score, ranked against each other rather than played head-to-head.
/// </summary>
/// <remarks>
/// A new table rather than columns bolted onto <c>matches</c>: a performance
/// has one side, not two, and forcing it through a shape built for two teams
/// would mean a match with an always-null away team, or a fabricated
/// opponent that never existed. The arity is genuinely different, so it gets
/// a table of its own.
/// </remarks>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260906010247_AddPerformances")]
public sealed class AddPerformances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906010247_AddPerformances.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906010247_AddPerformances.Down.sql"));
}
