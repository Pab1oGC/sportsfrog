using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Puts back the public-portal restriction to the organization frogtech-solutions (temporary), in the three functions that decide what the portal shows.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260923234614_ReapplyPublicPortalOrgRestriction")]
public sealed class ReapplyPublicPortalOrgRestriction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260923234614_ReapplyPublicPortalOrgRestriction.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260923234614_ReapplyPublicPortalOrgRestriction.Down.sql"));
}
