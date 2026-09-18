using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Refreshes the <c>competitions.settings</c> column comment: drops
/// <c>color_scheme</c> (dark mode was removed — the public page is light
/// only, everywhere, with no per-competition or per-visitor choice) and
/// documents <c>hero_gradient_to</c>, <c>show_logo_background</c>,
/// <c>content_figure</c> and <c>content_figure_color</c>, added to
/// <see cref="SportFrog.Domain.Competitions.PortalTheme"/> across earlier
/// changes that never got their own copy of this comment. No column
/// change — the settings are jsonb.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260917030000_RemoveColorSchemeDocumentPendingFields")]
public sealed class RemoveColorSchemeDocumentPendingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917030000_RemoveColorSchemeDocumentPendingFields.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260917030000_RemoveColorSchemeDocumentPendingFields.Down.sql"));
}
