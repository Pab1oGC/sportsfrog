using Npgsql;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// Builds the Npgsql data source.
///
/// Exists so the enum mapping is declared in exactly one place: the runtime
/// container, the design-time factory and the tests all build their
/// connection through here. A data source that maps the enums differently
/// from the model fails at the first query, not at startup.
/// </summary>
public static class SportFrogDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        builder.MapEnum<MembershipRole>("membership_role");

        return builder.Build();
    }
}
