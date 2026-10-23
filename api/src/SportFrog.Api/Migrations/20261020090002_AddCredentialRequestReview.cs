using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Lets the platform organization see and decide every organization's
/// pending credential request, through two SECURITY DEFINER functions
/// rather than through tenant_isolation.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020090002_AddCredentialRequestReview")]
public sealed class AddCredentialRequestReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090002_AddCredentialRequestReview.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090002_AddCredentialRequestReview.Down.sql"));
}
