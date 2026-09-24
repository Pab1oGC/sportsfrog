using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds <c>matches.repechage_branch</c>, so the public calendar can put each
/// half of a kyorugi repechage in its own section instead of interleaving
/// both by round number, which is only comparable within one half.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260922020000_AddRepechageBranch")]
public sealed class AddRepechageBranch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260922020000_AddRepechageBranch.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260922020000_AddRepechageBranch.Down.sql"));
}
