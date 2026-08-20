using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory. Migrations are applied with the schema owner user,
/// which isn't the one the application uses, so the connection string isn't
/// taken from the service container but from
/// <c>SPORTFROG_MIGRATIONS_CONNECTION</c> or, failing that, from the
/// development configuration.
/// </summary>
public sealed class SportFrogDbContextFactory : IDesignTimeDbContextFactory<SportFrogDbContext>
{
    private const string EnvironmentVariable = "SPORTFROG_MIGRATIONS_CONNECTION";

    public SportFrogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("Migrations")
                ?? configuration.GetConnectionString("Default");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {EnvironmentVariable} or 'ConnectionStrings:Migrations' " +
                "to run the EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql(SportFrogDataSource.Create(connectionString), SportFrogDataSource.MapEnums)
            // This project defines its schema in hand-written SQL and never
            // runs `dotnet ef migrations add`, so the model snapshot EF
            // compares against is never regenerated and will always disagree
            // with the mapped entities. That disagreement is the design here,
            // not a missing migration, and without this every
            // `database update` would refuse to run.
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new SportFrogDbContext(options);
    }
}
