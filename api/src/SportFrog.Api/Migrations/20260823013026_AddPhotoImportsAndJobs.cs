using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Records batches of uploaded photographs, and gives the system a job queue.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260823013026_AddPhotoImportsAndJobs")]
public sealed class AddPhotoImportsAndJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260823013026_AddPhotoImportsAndJobs.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260823013026_AddPhotoImportsAndJobs.Down.sql"));
}
