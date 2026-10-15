using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds course_enrollments: which students are registered to which
/// courses, with their own fee exceptions.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013035252_AddCourseEnrollments")]
public sealed class AddCourseEnrollments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013035252_AddCourseEnrollments.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013035252_AddCourseEnrollments.Down.sql"));
}
