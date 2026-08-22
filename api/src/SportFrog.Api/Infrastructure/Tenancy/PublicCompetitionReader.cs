using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// The public view's way into the data: resolve the address, establish the
/// isolation context, read (DD-08).
///
/// The public path has no session, and the isolation mechanism needs an
/// established organization — a combination that would otherwise return
/// nothing, since the system fails closed. Resolving the address first is
/// what makes anonymous access compatible with row-level policies, without
/// weakening them or carving out an exception.
///
/// Reads run as <c>sportfrog_public</c>, which holds no write permission at
/// all: the read-only condition of the public view is enforced by the engine
/// rather than by the absence of a write in the code.
/// </summary>
public sealed class PublicCompetitionReader(
    [FromKeyedServices(PublicCompetitionReader.PublicDataSourceKey)] NpgsqlDataSource dataSource)
{
    /// <summary>Key the read-only data source is registered under.</summary>
    public const string PublicDataSourceKey = "sportfrog_public";

    /// <summary>
    /// Resolves the address and, only if it names something publishable, runs
    /// the read inside a transaction scoped to that organization.
    /// </summary>
    /// <returns>
    /// The read's result, or <c>null</c> when the address does not name a
    /// publishable competition. The caller answers that with "no such
    /// resource": a suspended organization and a competition that was never
    /// published must be indistinguishable from one that does not exist.
    /// </returns>
    public async Task<TResult?> ReadAsync<TResult>(
        string organizationSlug,
        string competitionSlug,
        Func<SportFrogDbContext, PublicCompetition, Task<TResult>> read,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(competitionSlug);
        ArgumentNullException.ThrowIfNull(read);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Resolution and read share one transaction because the context set
        // between them lives exactly that long.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var competition = await ResolveAsync(
            connection, transaction, organizationSlug, competitionSlug, cancellationToken);

        if (competition is null)
        {
            return null;
        }

        await SetOrganizationAsync(
            connection, transaction, competition.OrganizationId, cancellationToken);

        await using var database = CreateContext(connection);
        await database.Database.UseTransactionAsync(transaction, cancellationToken);

        var result = await read(database, competition);

        // A read-only path still commits: it ends the transaction, and with
        // it the isolation context, before the connection returns to the pool.
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Asks the database to resolve the two address segments.
    /// </summary>
    /// <remarks>
    /// The lookup runs through a function owned by the schema owner because
    /// <c>competitions</c> carries an isolation policy and no context is
    /// established yet. The function is deliberately narrow — existence,
    /// activity, publishability — so running it with those privileges grants
    /// nothing else.
    /// </remarks>
    private static async Task<PublicCompetition?> ResolveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string organizationSlug,
        string competitionSlug,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT org_id, competition_id
            FROM resolve_public_competition($1::citext, $2::citext)
            """,
            connection,
            transaction);

        command.Parameters.Add(new NpgsqlParameter { Value = organizationSlug });
        command.Parameters.Add(new NpgsqlParameter { Value = competitionSlug });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new PublicCompetition(reader.GetGuid(0), reader.GetGuid(1))
            : null;
    }

    private static async Task SetOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.current_org', $1, true)", connection, transaction);

        command.Parameters.Add(new NpgsqlParameter { Value = organizationId.ToString() });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// A context over the connection already carrying the isolation context.
    /// Short-lived on purpose: it must not outlive the transaction that gives
    /// its queries their organization.
    /// </summary>
    private static SportFrogDbContext CreateContext(NpgsqlConnection connection)
    {
        var options = new DbContextOptionsBuilder<SportFrogDbContext>()
            .UseNpgsql(connection, SportFrogDataSource.MapEnums)
            .Options;

        return new SportFrogDbContext(options);
    }
}
