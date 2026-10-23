using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds credential_requests: what an organization raises instead of a
/// document batch now that only the platform organization may start one.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020090001_AddCredentialRequests")]
public sealed class AddCredentialRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090001_AddCredentialRequests.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020090001_AddCredentialRequests.Down.sql"));
}
