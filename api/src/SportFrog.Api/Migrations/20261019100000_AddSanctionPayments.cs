using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds sanction_payments: one or more payments against a sanction's fine,
/// reusing payment_method and receipt_review_state verbatim from
/// AddTuitionPayments — see this migration's own Up.sql for why neither is
/// forked into a second pair of identical labels.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261019100000_AddSanctionPayments")]
public sealed class AddSanctionPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261019100000_AddSanctionPayments.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261019100000_AddSanctionPayments.Down.sql"));
}
