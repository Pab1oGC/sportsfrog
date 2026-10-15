using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds the school module's first two tables: the courses an organization
/// runs and the weekly slots each one meets in.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013023520_AddSchoolCourses")]
public sealed class AddSchoolCourses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013023520_AddSchoolCourses.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013023520_AddSchoolCourses.Down.sql"));
}
