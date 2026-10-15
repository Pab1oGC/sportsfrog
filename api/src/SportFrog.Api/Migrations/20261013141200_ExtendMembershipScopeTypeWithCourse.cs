using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds 'course' to membership_scope_type. Alone, on purpose — see this
/// migration's own Up.sql for why nothing that uses the new value may share
/// its transaction.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261013141200_ExtendMembershipScopeTypeWithCourse")]
public sealed class ExtendMembershipScopeTypeWithCourse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013141200_ExtendMembershipScopeTypeWithCourse.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261013141200_ExtendMembershipScopeTypeWithCourse.Down.sql"));
}
