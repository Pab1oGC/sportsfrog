using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Gives volleyball a point target per set (25, 15 for the fifth) so
/// <see cref="SportFrog.Api.Features.Matches.SetsResultShape"/> can refuse a
/// set that reaches the right period count without reaching the right
/// score — see that class for the margin rule itself.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020120000_AddSetTargetPoints")]
public sealed class AddSetTargetPoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020120000_AddSetTargetPoints.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020120000_AddSetTargetPoints.Down.sql"));
}
