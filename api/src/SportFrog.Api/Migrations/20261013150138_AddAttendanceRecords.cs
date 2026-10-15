using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds attendance_records: one mark per enrollment per session.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013150138_AddAttendanceRecords")]
public sealed class AddAttendanceRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013150138_AddAttendanceRecords.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013150138_AddAttendanceRecords.Down.sql"));
}
