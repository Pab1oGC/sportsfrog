using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds published_issued_documents: the policy
/// ScopePublicReadsToPublishedCompetitions named but deliberately left
/// undone, so that re-enabling public credential verification is finally
/// what its own comment said it would be — a routing change with a working
/// policy behind it, not one that resolves a serial and then reads nothing
/// back. See this migration's own Up.sql for the full reasoning.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020080000_AddPublishedIssuedDocuments")]
public sealed class AddPublishedIssuedDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020080000_AddPublishedIssuedDocuments.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020080000_AddPublishedIssuedDocuments.Down.sql"));
}
