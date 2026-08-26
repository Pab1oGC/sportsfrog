using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Records batches of printed documents, and ties each document to its batch.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260824212621_AddDocumentBatches")]
public sealed class AddDocumentBatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824212621_AddDocumentBatches.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260824212621_AddDocumentBatches.Down.sql"));
}
