using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Removes the temporary <c>frogtech-solutions</c>-only restriction
/// <c>RestrictPublicPortalToFrogtech</c> added to the public portal: every
/// organization with a published competition is reachable again, the way
/// <c>AddPublicCompetitionDirectory</c> originally built it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260918010000_RemovePublicPortalOrgRestriction")]
public sealed class RemovePublicPortalOrgRestriction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918010000_RemovePublicPortalOrgRestriction.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918010000_RemovePublicPortalOrgRestriction.Down.sql"));
}
