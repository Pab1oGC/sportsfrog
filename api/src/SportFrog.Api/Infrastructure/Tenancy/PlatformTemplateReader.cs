using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Infrastructure.Tenancy;

/// <summary>
/// The platform organization's way to see which organization a document
/// design belongs to, regardless of which one it is.
/// </summary>
/// <remarks>
/// The same shape <see cref="PlatformDocumentRequestReader"/> already is
/// for a credential request, and for the same reason: updating or deleting a
/// template by id has to route into the organization that owns it before it
/// can act there at all, and that is a platform-level question
/// <see cref="OrganizationContext"/> cannot answer — it only ever answers
/// "what can this caller see inside the organization it is already acting
/// in". Goes through <c>document_template_org()</c>, a narrow
/// <c>SECURITY DEFINER</c> function restricted to a policy that applies to
/// nobody but the schema owner it runs as; see that migration's own remarks.
/// </remarks>
public static class PlatformTemplateReader
{
    /// <summary>
    /// The organization a template belongs to, or <c>null</c> if no template
    /// has that id. Answers regardless of whether the template was retired,
    /// the same reason <c>credential_request_org</c> answers regardless of a
    /// request's status: a caller routing into the right organization has to
    /// get there before the ordinary soft-delete filter gets a say.
    /// </summary>
    public static async Task<Guid?> ResolveOrganizationAsync(
        SportFrogDbContext database, Guid templateId, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT org_id FROM document_template_org($1)";
            command.Parameters.Add(new NpgsqlParameter { Value = templateId });

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is Guid organizationId ? organizationId : null;
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }
}
