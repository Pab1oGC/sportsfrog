using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Flags taekwondo kyorugi's <c>point</c> and <c>penalty</c> (gam-jeom)
/// metrics as score-affecting, so a bout's result can be finished from the
/// events already recorded live instead of typed in by hand — see
/// <see cref="SportFrog.Api.Features.Matches.RecordResult"/>.
/// </summary>
[DbContext(typeof(SportFrogDbContext))]
[Migration("20260918020000_AddTaekwondoKyorugiScoringEvents")]
public sealed class AddTaekwondoKyorugiScoringEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918020000_AddTaekwondoKyorugiScoringEvents.Up.sql"));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(EmbeddedSql.Read("20260918020000_AddTaekwondoKyorugiScoringEvents.Down.sql"));
}
