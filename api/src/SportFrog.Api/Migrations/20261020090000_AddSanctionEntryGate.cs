using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds sanction_blocks_entry: a bare boolean sportfrog_public can call to
/// learn whether a person is blocked by an unresolved sanction, without ever
/// being granted anything on sanctions itself — see this migration's own
/// Up.sql for the full reasoning and for the drift risk against
/// SanctionLedger.Resolve that comes with it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020090000_AddSanctionEntryGate")]
public sealed class AddSanctionEntryGate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090000_AddSanctionEntryGate.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090000_AddSanctionEntryGate.Down.sql"));
}
