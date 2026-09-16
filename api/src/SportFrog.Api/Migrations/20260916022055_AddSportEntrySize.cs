using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Says, per sport, how many athletes may make up one entry — so a Kyorugi
/// pair stops depending on nobody having typed the wrong roster cap.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260916022055_AddSportEntrySize")]
public sealed class AddSportEntrySize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260916022055_AddSportEntrySize.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260916022055_AddSportEntrySize.Down.sql"));
}
