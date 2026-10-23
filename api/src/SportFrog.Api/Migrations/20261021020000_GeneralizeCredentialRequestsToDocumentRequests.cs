using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Renames credential_requests to document_requests and gives it a kind,
/// so asking for a batch of certificates is the same request a credential
/// already was, not a second table beside it.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261021020000_GeneralizeCredentialRequestsToDocumentRequests")]
public sealed class GeneralizeCredentialRequestsToDocumentRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(
            EmbeddedSql.Read("20261021020000_GeneralizeCredentialRequestsToDocumentRequests.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(
            EmbeddedSql.Read("20261021020000_GeneralizeCredentialRequestsToDocumentRequests.Down.sql"));
}
