using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Gives a club an address to be told about its own fixtures at.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260909090000_AddClubContactEmail")]
public sealed class AddClubContactEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260909090000_AddClubContactEmail.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260909090000_AddClubContactEmail.Down.sql"));
}
