using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Lets anybody holding a credential check that its serial is real and valid.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260824215033_AddPublicDocumentVerification")]
public sealed class AddPublicDocumentVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824215033_AddPublicDocumentVerification.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824215033_AddPublicDocumentVerification.Down.sql"));
}
