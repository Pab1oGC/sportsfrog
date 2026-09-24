using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds <c>categories.uses_repechage</c> and <c>matches.is_repechage</c>, so
/// kyorugi's repechage can be turned on per category and its own ladder
/// matches can be told apart from the category's run at the title.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260922010000_AddKyorugiRepechage")]
public sealed class AddKyorugiRepechage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260922010000_AddKyorugiRepechage.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260922010000_AddKyorugiRepechage.Down.sql"));
}
