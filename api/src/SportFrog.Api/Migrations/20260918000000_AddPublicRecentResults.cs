using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds <c>list_public_recent_results</c> and the <c>published_matches</c>
/// row-level policy it depends on, so the landing hero can float real
/// results across every published competition — see
/// <see cref="SportFrog.Api.Features.Public.ReadPublicRecentResults"/>.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260918000000_AddPublicRecentResults")]
public sealed class AddPublicRecentResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918000000_AddPublicRecentResults.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918000000_AddPublicRecentResults.Down.sql"));
}
