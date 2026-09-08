using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// "One team per club per category" no longer holds for an individual
/// sport, where several athletes of the same delegation share both — see
/// <c>uq_teams_club_category</c>'s new predicate for why the column this adds
/// exists at all.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260906001143_RelaxTeamUniquenessForIndividualSports")]
public sealed class RelaxTeamUniquenessForIndividualSports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906001143_RelaxTeamUniquenessForIndividualSports.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260906001143_RelaxTeamUniquenessForIndividualSports.Down.sql"));
}
