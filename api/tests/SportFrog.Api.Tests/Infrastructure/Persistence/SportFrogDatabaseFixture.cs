using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SportFrog.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// A real PostgreSQL container, schema applied through the actual migration
/// path, with the same three roles production uses. Shared across every test
/// class in <see cref="SportFrogDatabaseCollection"/> so the container starts
/// once per test run, not once per class.
///
/// Isolation note: <c>organizations</c>, <c>users</c> and
/// <c>refresh_tokens</c> carry no row-level security policy. Every test
/// scopes its assertions to the GUIDs it created itself — never to a count or
/// a full-table read, which would also see rows from every other test.
/// </summary>
public sealed class SportFrogDatabaseFixture : IAsyncLifetime
{
    private const string OwnerUser = "sportfrog_owner";
    private const string OwnerPassword = "owner-test-password";
    private const string AppPassword = "app-test-password";
    private const string PublicPassword = "public-test-password";

    private readonly PostgreSqlContainer _container;

    private NpgsqlDataSource _appDataSource = null!;

    public string OwnerConnectionString { get; private set; } = null!;
    public string AppConnectionString { get; private set; } = null!;
    public string PublicConnectionString { get; private set; } = null!;

    public SportFrogDatabaseFixture()
    {
        var rolesScriptContent = File.ReadAllBytes(FindRepositoryFile("docker/postgres/init/01-roles.sh"));

        // 0755: owner read/write/execute, group and other read/execute.
        const uint executableFileMode = 0b111_101_101;

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("sportfrog_test")
            .WithEnvironment("SPORTFROG_OWNER_USER", OwnerUser)
            .WithEnvironment("SPORTFROG_OWNER_PASSWORD", OwnerPassword)
            .WithEnvironment("SPORTFROG_APP_PASSWORD", AppPassword)
            .WithEnvironment("SPORTFROG_PUBLIC_PASSWORD", PublicPassword)
            // Same script the docker-compose stack uses to create the three
            // roles: one source of truth, not a parallel reimplementation.
            // Passed as bytes, not as a source path: the (string, string)
            // overload copied the file in as a directory instead of a file
            // when this was tried against Testcontainers 4.13.0.
            .WithResourceMapping(
                rolesScriptContent,
                "/docker-entrypoint-initdb.d/01-roles.sh",
                executableFileMode)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        OwnerConnectionString = BuildConnectionString(OwnerUser, OwnerPassword);
        AppConnectionString = BuildConnectionString("sportfrog_app", AppPassword);
        PublicConnectionString = BuildConnectionString("sportfrog_public", PublicPassword);

        // Plain connection string, no enum mapping: membership_role doesn't
        // exist yet, and mapping against a type that isn't there is exactly
        // the ordering mistake this two-step init avoids.
        var migrationOptions = new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql(OwnerConnectionString)
            // This project defines its schema in hand-written SQL and never
            // runs `dotnet ef migrations add`, so the model snapshot EF
            // Core 7+ diffs against on every Migrate() is never regenerated
            // and will legitimately disagree with OnModelCreating. That's
            // by design here, not a bug this fixture should fail on.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        // SeedPlatformOwner (one of the migrations this run applies) reads
        // these two from the process environment exactly like a real
        // deployment would — there is no test-only branch in that migration,
        // on purpose, so this proves the same code path production runs.
        Environment.SetEnvironmentVariable("PLATFORM_ADMIN_EMAIL", "owner@frogtech-solutions.test");
        Environment.SetEnvironmentVariable("PLATFORM_ADMIN_PASSWORD", "Test-Owner-Password-1!");

        await using (var migrationContext = new SportFrogDbContext(migrationOptions))
        {
            // The real migration path: InitialSchema + SeedCatalog, exactly
            // as production applies them. Also proves the migrations
            // themselves actually run, not just that the SQL is well-formed.
            await migrationContext.Database.MigrateAsync();
        }

        // Built only now, after the schema exists: MapEnum resolves
        // membership_role against pg_type on this data source's first use.
        // Built once and reused, matching how the app would hold a single
        // NpgsqlDataSource for its lifetime.
        _appDataSource = SportFrogDataSource.Create(AppConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _appDataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    public SportFrogDbContext CreateAppContext()
    {
        var options = new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql(_appDataSource, SportFrogDataSource.MapEnums)
            .Options;

        return new SportFrogDbContext(options);
    }

    /// <summary>
    /// Sets the isolation context for the current transaction only — the
    /// exact equivalent of <c>SET LOCAL</c>, safe to parameterize unlike a
    /// literal <c>SET LOCAL</c> statement. Must run inside the same
    /// transaction as whatever RLS-protected read or write depends on it:
    /// once that transaction commits or rolls back, the setting is gone.
    /// </summary>
    public static Task SetCurrentOrganizationAsync(SportFrogDbContext context, Guid organizationId) =>
        context.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_org', {0}, true)", organizationId.ToString());

    private string BuildConnectionString(string username, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = username,
            Password = password,
        };
        return builder.ConnectionString;
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                $"Could not locate '{relativePath}' above {AppContext.BaseDirectory}.")
            : Path.Combine(directory.FullName, relativePath);
    }
}
