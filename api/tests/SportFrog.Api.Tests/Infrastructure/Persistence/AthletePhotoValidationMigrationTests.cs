using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// The migration that adds photo validation can be applied, reverted by its
/// own <c>.Down.sql</c>, and applied again. The README requires this for every
/// migration: a failed deployment has to be undoable without recreating the
/// database.
/// </summary>
/// <remarks>
/// Runs in a database of its own, created inside the test container, so the
/// columns can be dropped without touching the database the other tests share.
/// </remarks>
[Collection(nameof(SportFrogDatabaseCollection))]
public sealed class AthletePhotoValidationMigrationTests(SportFrogDatabaseFixture fixture)
{
    private const string PreviousMigration = "20261005175603_CredentialDesigns";

    private static readonly string[] PhotoValidationColumns =
    [
        "photo_validation_state",
        "photo_validation_reasons",
        "photo_validation_warnings",
        "photo_rules_version",
        "photo_validated_at",
    ];

    [Fact]
    public async Task The_migration_applies_reverts_and_applies_again()
    {
        var database = $"sportfrog_rollback_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(database);

        try
        {
            var connectionString = ConnectionStringFor(database);

            // Everything up to the previous migration: the photo columns must not exist yet.
            await MigrateAsync(connectionString, PreviousMigration);
            (await CountPhotoColumnsAsync(connectionString)).Should().Be(0);
            (await PhotoValidationTypeExistsAsync(connectionString)).Should().BeFalse();

            // Applying it adds all five columns and the enum type.
            await MigrateAsync(connectionString);
            (await CountPhotoColumnsAsync(connectionString)).Should().Be(PhotoValidationColumns.Length);
            (await PhotoValidationTypeExistsAsync(connectionString)).Should().BeTrue();

            // Reverting it runs the .Down.sql: columns and enum type are gone again.
            await MigrateAsync(connectionString, PreviousMigration);
            (await CountPhotoColumnsAsync(connectionString)).Should().Be(0);
            (await PhotoValidationTypeExistsAsync(connectionString)).Should().BeFalse();

            // And it applies again cleanly after the rollback.
            await MigrateAsync(connectionString);
            (await CountPhotoColumnsAsync(connectionString)).Should().Be(PhotoValidationColumns.Length);
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

    private static async Task<int> CountPhotoColumnsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'athletes'
              AND column_name = ANY(@columns)
            """,
            connection);
        command.Parameters.AddWithValue("columns", PhotoValidationColumns);

        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> PhotoValidationTypeExistsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'photo_validation_state')",
            connection);

        return (bool)(await command.ExecuteScalarAsync())!;
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
