using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds team_staff: the people a team has who do not play for it — the bench
/// and the board — so they can be accredited and printed a credential. See
/// this migration's own Up.sql for why they are not roster entries with a
/// flag.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261017120000_AddTeamStaff")]
public sealed class AddTeamStaff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261017120000_AddTeamStaff.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261017120000_AddTeamStaff.Down.sql"));
}
