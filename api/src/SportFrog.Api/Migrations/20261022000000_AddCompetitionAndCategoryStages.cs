using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// The stages a competition's own draw already implies — a group stage, a
/// bracket, a classification round — as rows of their own, so what belongs
/// to one of them stops being forced to live on the whole competition or
/// the whole category.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261022000000_AddCompetitionAndCategoryStages")]
public sealed class AddCompetitionAndCategoryStages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261022000000_AddCompetitionAndCategoryStages.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261022000000_AddCompetitionAndCategoryStages.Down.sql"));
}
