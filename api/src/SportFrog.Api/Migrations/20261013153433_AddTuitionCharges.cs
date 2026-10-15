using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds tuition_charges: one month's pension per enrollment, with no
/// status column — see this migration's own Up.sql for why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013153433_AddTuitionCharges")]
public sealed class AddTuitionCharges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013153433_AddTuitionCharges.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013153433_AddTuitionCharges.Down.sql"));
}
