using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds sanctions: a disciplinary sanction against one person, cleared by a
/// number of matches, by a fine, or by both. See this migration's own Up.sql
/// for why status is never a column and why withholding a credential is not
/// the same as revoking it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261018090000_AddSanctions")]
public sealed class AddSanctions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261018090000_AddSanctions.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261018090000_AddSanctions.Down.sql"));
}
