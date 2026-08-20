using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Makes a ruleset name unique within its organization.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260820233218_AddRulesetNameUniqueness")]
public sealed class AddRulesetNameUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260820233218_AddRulesetNameUniqueness.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260820233218_AddRulesetNameUniqueness.Down.sql"));
}
