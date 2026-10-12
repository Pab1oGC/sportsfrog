using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds a second, optional axis to authorization: a membership with no rows
/// here reaches its role everywhere in the organization, exactly as today;
/// one with rows reaches it only inside the competitions and clubs those
/// rows name (RF-02's delegates and event administrators).
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261008021212_AddMembershipScopes")]
public sealed class AddMembershipScopes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261008021212_AddMembershipScopes.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261008021212_AddMembershipScopes.Down.sql"));
}
