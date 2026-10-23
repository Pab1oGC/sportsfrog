using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds the single-use tokens that let someone who has forgotten their
/// password set a new one (RF-02).
///
/// <see cref="SportFrog.Api.Features.Auth.ChangePassword"/> verifies the
/// current password before it changes anything, so it can serve only an
/// account whose password is still known. This table is the proof that
/// stands in for it: mailed to the registered address, redeemable once.
/// See the migration's SQL for the shape and for why it is stored as a
/// digest.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261020130000_AddPasswordResetTokens")]
public sealed class AddPasswordResetTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020130000_AddPasswordResetTokens.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261020130000_AddPasswordResetTokens.Down.sql"));
}
