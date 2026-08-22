using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// TODO: describe in one line what this migration changes and why.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260819031901_FixPublicCompetitionResolution")]
public sealed class FixPublicCompetitionResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260819031901_FixPublicCompetitionResolution.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260819031901_FixPublicCompetitionResolution.Down.sql"));
}
