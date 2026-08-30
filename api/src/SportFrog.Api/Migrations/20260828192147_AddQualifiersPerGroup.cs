using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Adds <c>categories.qualifiers_per_group</c>, for the public table to highlight who advances.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260828192147_AddQualifiersPerGroup")]
public sealed class AddQualifiersPerGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828192147_AddQualifiersPerGroup.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260828192147_AddQualifiersPerGroup.Down.sql"));
}
