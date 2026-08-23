using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Infrastructure.Jobs;

/// <summary>
/// Lets work that has no request behind it act inside an organization.
/// </summary>
/// <remarks>
/// This is the single most important object in the background-work story,
/// and the reason is easy to miss. Isolation in this system is enforced by
/// the database against <c>app.current_org</c>, which the entry channel sets
/// per transaction from a validated token (DD-02, DD-07). A job has no token,
/// no request and no entry channel. Written the obvious way it would run with
/// no isolation context at all — and Postgres does not answer that with an
/// error, it answers it with an empty result. A batch would quietly do
/// nothing and report success.
///
/// So the organization travels as an argument on the job itself, and this
/// establishes it before any work runs. The argument is a claim, not proof:
/// row-level security is still what checks it, and a job pointed at a batch
/// belonging to somebody else simply finds no batch.
///
/// The person is carried too, because a change with no author is a change the
/// audit log cannot file (RNF-12). The photographs a batch attaches were
/// asked for by whoever uploaded the archive, and that is who the log names —
/// not the worker process.
/// </remarks>
public sealed class OrganizationJobScope(IServiceScopeFactory scopes)
{
    /// <summary>
    /// Runs one unit of work in its own scope and its own transaction, with
    /// the isolation context set for that transaction.
    /// </summary>
    /// <remarks>
    /// A job calls this several times rather than once around everything: the
    /// slow part of a batch is decoding and uploading images, which has no
    /// business holding a database transaction open for minutes. Reading what
    /// to do is one transaction, recording what was done is another.
    /// </remarks>
    public async Task<T> RunAsync<T>(
        Guid organizationId,
        Guid userId,
        Func<IServiceProvider, SportFrogDbContext, Task<T>> work,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        Establish(scope.ServiceProvider, organizationId, userId);

        var database = scope.ServiceProvider.GetRequiredService<SportFrogDbContext>();

        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // Exactly what the entry channel does for a request. set_config with
        // is_local true is SET LOCAL: it lasts for this transaction and never
        // for the pooled connection, which is what keeps one organization's
        // context from being inherited by whoever borrows the connection next.
        await database.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_org', {0}, true)",
            [organizationId.ToString()],
            cancellationToken);

        try
        {
            var result = await work(scope.ServiceProvider, database);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Runs work that touches no database, with the organization established
    /// all the same.
    /// </summary>
    /// <remarks>
    /// For the slow middle of a batch: decoding images and putting them in
    /// object storage. Those need to know which organization they belong to —
    /// object storage has no row-level security to fall back on, only the key
    /// prefix — but holding a transaction open across them would be a
    /// long-running lock in exchange for nothing.
    /// </remarks>
    public async Task<T> RunDetachedAsync<T>(
        Guid organizationId,
        Guid userId,
        Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = scopes.CreateAsyncScope();

        Establish(scope.ServiceProvider, organizationId, userId);

        return await work(scope.ServiceProvider);
    }

    /// <summary>
    /// Puts the organization and the person into the scope, the way the entry
    /// channel does for a request.
    /// </summary>
    /// <remarks>
    /// The role is the highest one, because a job is not a person clicking:
    /// it is work an operator already asked for and was already allowed to
    /// ask for, running later. Refusing it now on a role check would be
    /// refusing a decision that has already been made.
    /// </remarks>
    private static void Establish(IServiceProvider services, Guid organizationId, Guid userId) =>
        services.GetRequiredService<OrganizationContext>()
            .Establish(organizationId, MembershipRole.Owner, userId, ipAddress: null);
}
