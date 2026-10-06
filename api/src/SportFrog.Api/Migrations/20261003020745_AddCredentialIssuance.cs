using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Lets a document batch and an issued document print as a decreed
/// credential — no template, a frozen catalogue snapshot instead, and a
/// sequential visible id claimed from a per-competition counter.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261003020745_AddCredentialIssuance")]
public sealed class AddCredentialIssuance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261003020745_AddCredentialIssuance.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261003020745_AddCredentialIssuance.Down.sql"));
}
