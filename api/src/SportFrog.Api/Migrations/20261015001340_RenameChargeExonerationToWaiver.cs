using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Renames tuition_charges' exonerated_at/exoneration_reason columns (and
/// their CHECK constraint) to waived_at/waiver_reason — "condonar" reads
/// clearer to staff than "exonerar" did. No data moves.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261015001340_RenameChargeExonerationToWaiver")]
public sealed class RenameChargeExonerationToWaiver : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261015001340_RenameChargeExonerationToWaiver.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261015001340_RenameChargeExonerationToWaiver.Down.sql"));
}
