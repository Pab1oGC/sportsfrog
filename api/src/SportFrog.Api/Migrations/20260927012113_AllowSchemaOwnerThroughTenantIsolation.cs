using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Fixes a regression from <c>ScopePublicReadsToPublishedCompetitions</c>:
/// narrowing <c>tenant_isolation</c> to <c>sportfrog_app</c> also silently
/// cut off <c>sportfrog_owner</c>, the role the <c>SECURITY DEFINER</c>
/// functions <c>resolve_public_competition</c>/<c>resolve_public_document</c>
/// execute as. Under <c>FORCE ROW LEVEL SECURITY</c> the schema owner needs
/// an applicable policy too; this adds it back without ever re-admitting
/// <c>sportfrog_public</c>.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260927012113_AllowSchemaOwnerThroughTenantIsolation")]
public sealed class AllowSchemaOwnerThroughTenantIsolation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260927012113_AllowSchemaOwnerThroughTenantIsolation.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260927012113_AllowSchemaOwnerThroughTenantIsolation.Down.sql"));
}
