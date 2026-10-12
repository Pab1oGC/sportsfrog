using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Empties the sponsors of the demo competitions that were seeded without a
/// logo key, which made the competitions listing fail to deserialize.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261012010000_FixDemoCompetitionSponsors")]
public sealed class FixDemoCompetitionSponsors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261012010000_FixDemoCompetitionSponsors.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261012010000_FixDemoCompetitionSponsors.Down.sql"));
}
