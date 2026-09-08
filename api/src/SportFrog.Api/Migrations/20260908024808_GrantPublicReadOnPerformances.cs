using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Grants sportfrog_public SELECT on performances, so the public portal's
/// classification page can read a judged category's classification stage.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260908024808_GrantPublicReadOnPerformances")]
public sealed class GrantPublicReadOnPerformances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260908024808_GrantPublicReadOnPerformances.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260908024808_GrantPublicReadOnPerformances.Down.sql"));
}
