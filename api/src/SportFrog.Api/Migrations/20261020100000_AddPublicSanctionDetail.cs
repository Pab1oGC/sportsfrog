using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds public_active_sanctions: sportfrog_public's window into which kind
/// of sanction blocks a person -- by matches, by a fine, or both, how much
/// of each remains -- never the free-text reason. See this migration's own
/// Up.sql for the full reasoning, and for why sanction_blocks_entry
/// (AddSanctionEntryGate) is redefined here to call it instead of carrying
/// its own copy of the same condition.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020100000_AddPublicSanctionDetail")]
public sealed class AddPublicSanctionDetail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020100000_AddPublicSanctionDetail.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020100000_AddPublicSanctionDetail.Down.sql"));
}
