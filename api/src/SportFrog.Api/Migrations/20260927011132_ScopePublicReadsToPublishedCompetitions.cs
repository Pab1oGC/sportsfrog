using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Narrows <c>tenant_isolation</c> to <c>sportfrog_app</c> on every table
/// that has it, and adds a <c>published_*</c> policy for the eight tables
/// the anonymous public role actually needs (roster names, top-scorer
/// names, venues, rulesets). Before this, <c>sportfrog_public</c> satisfied
/// <c>tenant_isolation</c> like any other role, so setting
/// <c>app.current_org</c> to any organization's id — freely discoverable,
/// since <c>organizations</c> carries no RLS — read that organization's
/// full rows regardless of publication, athletes' document id, birth date
/// and guardian phone included.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260927011132_ScopePublicReadsToPublishedCompetitions")]
public sealed class ScopePublicReadsToPublishedCompetitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260927011132_ScopePublicReadsToPublishedCompetitions.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260927011132_ScopePublicReadsToPublishedCompetitions.Down.sql"));
}
