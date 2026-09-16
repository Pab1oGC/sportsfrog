using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>Gives a venue a Google Maps link, so a visitor can be shown the way there.</summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260914030434_AddVenueMapsUrl")]
public sealed class AddVenueMapsUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260914030434_AddVenueMapsUrl.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260914030434_AddVenueMapsUrl.Down.sql"));
}
