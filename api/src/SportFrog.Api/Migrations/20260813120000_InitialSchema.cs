using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Full schema: extensions, enum types, tables, indexes, constraints,
/// row-level security policies, application roles with their grants, the
/// public resolution function, and the triggers.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260813120000_InitialSchema")]
public sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260813120000_InitialSchema.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260813120000_InitialSchema.Down.sql"));
}
