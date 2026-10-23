using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Removes the "frogtech-solutions only" restriction that
/// <c>ReapplyPublicPortalOrgRestriction</c> put back: every organization with
/// a published competition is reachable through the public portal again,
/// permanently this time — no further migration is expected to re-apply it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261019132539_RemovePublicPortalFrogtechRestriction")]
public sealed class RemovePublicPortalFrogtechRestriction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261019132539_RemovePublicPortalFrogtechRestriction.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261019132539_RemovePublicPortalFrogtechRestriction.Down.sql"));
}
