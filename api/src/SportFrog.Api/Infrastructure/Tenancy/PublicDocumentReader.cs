using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>The document an address resolved to.</summary>
public sealed record PublicDocument(Guid OrganizationId, Guid DocumentId);

/// <summary>
/// The way in for somebody holding a printed document.
/// </summary>
/// <remarks>
/// The same three steps the public competition view takes — resolve the
/// address, establish the isolation context, read — and for the same reason:
/// there is no session, and the isolation mechanism needs an organization, so
/// resolving the address first is what makes anonymous access compatible with
/// the row-level policies instead of an exception to them.
///
/// It is a separate reader rather than another method on the competition one
/// because the two resolve different things and, more importantly, resolve
/// under different conditions. That one requires a competition somebody chose
/// to publish. This one must work whether or not anybody published anything:
/// a credential is valid because it was issued.
///
/// Reads run as <c>sportfrog_public</c>, which holds no write permission at
/// all — so the one endpoint on this API that anybody on the internet can
/// reach cannot alter a document even if this code were wrong about it.
/// </remarks>
public sealed class PublicDocumentReader(
    [FromKeyedServices(PublicCompetitionReader.PublicDataSourceKey)] NpgsqlDataSource dataSource)
{
    /// <summary>
    /// Resolves a printed serial and reads what it names.
    /// </summary>
    /// <returns>
    /// The read's result, or <c>null</c> when the address names nothing. A
    /// serial that was never issued and one belonging to an organization that
    /// no longer exists must be indistinguishable.
    /// </returns>
    public async Task<TResult?> ReadAsync<TResult>(
        string organizationSlug,
        string serial,
        Func<SportFrogDbContext, PublicDocument, Task<TResult>> read,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationSlug);
        ArgumentNullException.ThrowIfNull(read);

        if (string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // Resolution and read share one transaction because the context set
        // between them lives exactly that long.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var document = await ResolveAsync(
            connection, transaction, organizationSlug, serial, cancellationToken);

        if (document is null)
        {
            return null;
        }

        await using var database = CreateContext(connection);
        await database.Database.UseTransactionAsync(transaction, cancellationToken);

        var result = await read(database, document);

        // A read-only path still commits: it ends the transaction, and with it
        // the isolation context, before the connection returns to the pool.
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Asks the database to resolve the slug and the serial.
    /// </summary>
    /// <remarks>
    /// Through a function owned by the schema owner, because
    /// <c>issued_documents</c> carries an isolation policy and no context is
    /// established yet. The function establishes it as its last act, which is
    /// why nothing after this needs privileges of its own.
    /// </remarks>
    private static async Task<PublicDocument?> ResolveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string organizationSlug,
        string serial,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT org_id, document_id
            FROM resolve_public_document($1::citext, $2::text)
            """,
            connection,
            transaction);

        command.Parameters.Add(new NpgsqlParameter { Value = organizationSlug });
        command.Parameters.Add(new NpgsqlParameter { Value = serial });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new PublicDocument(reader.GetGuid(0), reader.GetGuid(1))
            : null;
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
