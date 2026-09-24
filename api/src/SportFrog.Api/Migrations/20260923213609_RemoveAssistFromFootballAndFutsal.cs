using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Removes the "assist" metric from football and futsal, and from every
/// ruleset that listed it, so the recorder no longer offers it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260923213609_RemoveAssistFromFootballAndFutsal")]
public sealed class RemoveAssistFromFootballAndFutsal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260923213609_RemoveAssistFromFootballAndFutsal.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260923213609_RemoveAssistFromFootballAndFutsal.Down.sql"));
}
