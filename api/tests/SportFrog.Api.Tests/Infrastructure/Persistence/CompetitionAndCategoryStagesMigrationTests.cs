using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// The migration that adds <c>competition_stages</c> and
/// <c>category_stages</c> can be applied, reverted by its own
/// <c>.Down.sql</c>, and applied again — the same requirement the README
/// states for every migration, and the same shape
/// <c>AthletePhotoValidationMigrationTests</c> already checks for one that
/// adds columns rather than tables.
/// </summary>
/// <remarks>
/// Runs in a database of its own, created inside the test container, so the
/// tables can be dropped without touching the database the other tests
/// share.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class CompetitionAndCategoryStagesMigrationTests(SportFrogDatabaseFixture fixture)
{
    private const string PreviousMigration = "20261021020000_GeneralizeCredentialRequestsToDocumentRequests";

    private static readonly string[] Tables = ["competition_stages", "category_stages"];

    [Fact]
    public async Task The_migration_applies_reverts_and_applies_again()
    {
        var database = $"sportfrog_rollback_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(database);

        try
        {
            var connectionString = ConnectionStringFor(database);

            // Everything up to the previous migration: neither table exists yet.
            await MigrateAsync(connectionString, PreviousMigration);
            (await CountTablesAsync(connectionString)).Should().Be(0);

            // Applying it creates both tables.
            await MigrateAsync(connectionString);
            (await CountTablesAsync(connectionString)).Should().Be(Tables.Length);

            // Reverting it runs the .Down.sql: both tables are gone again.
            await MigrateAsync(connectionString, PreviousMigration);
            (await CountTablesAsync(connectionString)).Should().Be(0);

            // And it applies again cleanly after the rollback.
            await MigrateAsync(connectionString);
            (await CountTablesAsync(connectionString)).Should().Be(Tables.Length);
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    private static async Task MigrateAsync(string connectionString, string? targetMigration = null)
    {
        await using var context = new SportFrogDbContext(OptionsFor(connectionString));
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private static DbContextOptions<SportFrogDbContext> OptionsFor(string connectionString) =>
        new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql(connectionString)
            // Same as the fixture: the schema is SQL, so the model snapshot is
            // not regenerated and a pending-changes warning is expected.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

    private static async Task<int> CountTablesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY(@tables)
            """,
            connection);
        command.Parameters.AddWithValue("tables", Tables);

        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task CreateDatabaseAsync(string name)
    {
        await using var connection = new NpgsqlConnection(ServerConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync(string name)
    {
        await using var connection = new NpgsqlConnection(ServerConnectionString());
        await connection.OpenAsync();

        // FORCE ends any connection still open to it, so a failed assertion
        // cannot leave the database behind for the next run.
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private string ServerConnectionString() =>
        new NpgsqlConnectionStringBuilder(fixture.SuperuserConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(fixture.SuperuserConnectionString)
        {
            Database = database,
        }.ConnectionString;
}
