using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds course_sessions: the actual dates a course's weekly schedule
/// materializes into.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013143925_AddCourseSessions")]
public sealed class AddCourseSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013143925_AddCourseSessions.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013143925_AddCourseSessions.Down.sql"));
}
