using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds tuition_payments: one or more payments against a tuition charge,
/// each with its own method and review state — see this migration's own
/// Up.sql for why neither is derived onto the charge.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013210929_AddTuitionPayments")]
public sealed class AddTuitionPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013210929_AddTuitionPayments.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013210929_AddTuitionPayments.Down.sql"));
}
