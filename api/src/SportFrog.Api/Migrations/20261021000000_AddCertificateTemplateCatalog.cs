using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Adds document_templates.is_catalog and the read policies that let every
/// organization name a certificate design the platform organization shared,
/// alongside the designs it made itself.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20261021000000_AddCertificateTemplateCatalog")]
public sealed class AddCertificateTemplateCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261021000000_AddCertificateTemplateCatalog.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20261021000000_AddCertificateTemplateCatalog.Down.sql"));
}
