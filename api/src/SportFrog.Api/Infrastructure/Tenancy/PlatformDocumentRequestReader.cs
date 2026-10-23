using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// The platform organization's way to see a document request regardless of
/// which organization it belongs to.
/// </summary>
/// <remarks>
/// <see cref="OrganizationContext"/> and <c>tenant_isolation</c> both answer
/// "what can this caller see inside the organization it is acting in" —
/// reviewing a request is a platform-level question, the same shape
/// <c>PlatformAuthority</c> already is for creating an organization, except
/// this one has to cross into data rather than stay outside all of it.
///
/// Both reads below go through <c>read_pending_document_requests()</c> and
/// <c>document_request_org()</c>: narrow <c>SECURITY DEFINER</c> functions,
/// exactly the shape <see cref="PublicCompetitionReader"/> already uses to
/// cross the same policy for the public portal, restricted here to a policy
/// that applies to nobody but the schema owner those functions run as (see
/// that migration's own remarks, and <c>AllowSchemaOwnerThroughTenantIsolation</c>
/// for why that role is safe to grant it to).
/// </remarks>
public static class PlatformDocumentRequestReader
{
    /// <summary>One organization's pending ask, named rather than just an id.</summary>
    public sealed record Pending(
        Guid Id,
        Guid OrgId,
        string OrgName,
        string OrgSlug,
        DocumentKind Kind,
        Guid? TemplateId,
        string? CertificateType,
        Guid CompetitionId,
        Guid? CategoryId,
        Guid? TeamId,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        Guid RequestedBy,
        DateTimeOffset CreatedAt);

    /// <summary>Every organization's pending request, oldest first.</summary>
    public static async Task<IReadOnlyList<Pending>> ReadPendingAsync(
        SportFrogDbContext database, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM read_pending_document_requests()";

            var results = new List<Pending>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new Pending(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    WireEnum.Parse<DocumentKind>(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetGuid(7),
                    reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    reader.IsDBNull(9) ? null : reader.GetGuid(9),
                    reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10),
                    reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11),
                    reader.GetGuid(12),
                    reader.GetFieldValue<DateTimeOffset>(13)));
            }

            return results;
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// The organization a request belongs to, or <c>null</c> if no request
    /// has that id. Answers regardless of the request's own status, which is
    /// what lets a caller route into the right organization before reading
    /// the row for real — and tell "no such request" from "already decided"
    /// once it does.
    /// </summary>
    public static async Task<Guid?> ResolveOrganizationAsync(
        SportFrogDbContext database, Guid requestId, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT org_id FROM document_request_org($1)";
            command.Parameters.Add(new NpgsqlParameter { Value = requestId });

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is Guid organizationId ? organizationId : null;
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }
}
