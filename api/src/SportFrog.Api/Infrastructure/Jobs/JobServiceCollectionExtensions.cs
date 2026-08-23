using Hangfire;
using Hangfire.PostgreSql;

namespace SportFrog.Api.Infrastructure.Jobs;

/// <summary>Registers the background job queue and its worker.</summary>
/// <remarks>
/// The queue lives in the same database as everything else, in its own
/// schema. That is a deliberate choice over a second piece of infrastructure:
/// a job that has to be enqueued in the same breath as a row is written is
/// only reliable if both are the same transaction, and a separate broker
/// cannot be.
///
/// The worker runs in the API process. At this size that is right — one
/// process, one deployment — and nothing here depends on it: the jobs take
/// their arguments and read their own state, so moving the worker into its
/// own process later is a hosting change and not a code one.
/// </remarks>
public static class JobServiceCollectionExtensions
{
    /// <summary>
    /// Where Hangfire keeps its tables. Created by migration, because the
    /// application user may not touch the business schema.
    /// </summary>
    private const string Schema = "hangfire";

    public static IServiceCollection AddSportFrogJobs(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                postgres => postgres.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = Schema,

                    // The schema itself is created by a migration, run by the
                    // schema owner. Hangfire builds its own tables inside it
                    // the first time it starts, which is why the application
                    // user holds CREATE on that one schema and nowhere else.
                    PrepareSchemaIfNecessary = true,
                }));

        services.AddHangfireServer(options =>
        {
            options.ServerName = "sportfrog";

            // Small on purpose. The work is image processing, which is
            // CPU-bound and already parallel across whatever cores there are;
            // twenty workers competing for them would make each batch slower
            // and every request served by the same process slower with it.
            options.WorkerCount = 2;
        });

        services.AddSingleton<OrganizationJobScope>();

        return services;
    }
}
