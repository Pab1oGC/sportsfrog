using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// Declares how the database's enum types map to CLR enums, in exactly one
/// place: the runtime container, the design-time factory and the tests all
/// configure their connection through here.
///
/// Both halves are required and neither substitutes for the other:
/// <see cref="Create"/> teaches the driver how to read and write the values,
/// and <see cref="MapEnums"/> teaches EF Core that the column holds that
/// enum type rather than an integer. Declaring only the first one compiles,
/// starts up and then fails at the first write with "column is of type
/// membership_role but expression is of type integer".
/// </summary>
public static class SportFrogDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        builder.MapEnum<MembershipRole>("membership_role");
        builder.MapEnum<CompetitionState>("competition_state");
        builder.MapEnum<CaptureLevel>("capture_level");
        builder.MapEnum<MatchState>("match_state");
        builder.MapEnum<PhotoImportState>("photo_import_state");
        builder.MapEnum<DocumentKind>("document_kind");
        builder.MapEnum<DocumentState>("document_state");
        builder.MapEnum<DocumentBatchState>("document_batch_state");

        return builder.Build();
    }

    /// <summary>
    /// Applies the same enum mapping to EF Core's own type resolution. Pass
    /// it to the <c>UseNpgsql</c> overload that takes an options action.
    /// </summary>
    public static void MapEnums(NpgsqlDbContextOptionsBuilder builder)
    {
        builder.MapEnum<MembershipRole>("membership_role");
        builder.MapEnum<CompetitionState>("competition_state");
        builder.MapEnum<CaptureLevel>("capture_level");
        builder.MapEnum<MatchState>("match_state");
        builder.MapEnum<PhotoImportState>("photo_import_state");
        builder.MapEnum<DocumentKind>("document_kind");
        builder.MapEnum<DocumentState>("document_state");
        builder.MapEnum<DocumentBatchState>("document_batch_state");
    }
}
