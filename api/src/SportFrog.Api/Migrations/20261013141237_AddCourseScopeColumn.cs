using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds the course_id column and widens ck_membership_scope_target to a
/// third branch, now that 'course' is a settled enum label.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013141237_AddCourseScopeColumn")]
public sealed class AddCourseScopeColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013141237_AddCourseScopeColumn.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013141237_AddCourseScopeColumn.Down.sql"));
}
