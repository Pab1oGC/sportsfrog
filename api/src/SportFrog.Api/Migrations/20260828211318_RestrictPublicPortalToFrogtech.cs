using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Temporary: narrows the public portal to one organization while the rest of
/// the platform's test data is still sitting in this database. See the SQL
/// for the reason and how to lift it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260828211318_RestrictPublicPortalToFrogtech")]
public sealed class RestrictPublicPortalToFrogtech : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828211318_RestrictPublicPortalToFrogtech.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828211318_RestrictPublicPortalToFrogtech.Down.sql"));
}
