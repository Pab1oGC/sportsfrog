using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Infrastructure.Persistence;

/// <summary>
/// Records every change to business data as it is saved (RNF-12, DD-07).
///
/// It sits at the save point rather than being called by each feature, and
/// that placement is the whole idea: it records what was actually persisted
/// instead of what an operation declared it would do, so a feature written
/// next month is audited without its author having to remember. A log that
/// depends on being invoked is a log with holes in it, and the holes are
/// exactly where nobody was thinking about auditing.
/// </summary>
public sealed class AuditInterceptor(OrganizationContext organization) : SaveChangesInterceptor
{
    /// <summary>
    /// Column names whose values never reach the log. A password digest or a
    /// session token recorded here would turn the audit trail into a second
    /// place those have to be protected.
    /// </summary>
    private static readonly string[] SecretMarkers = ["password", "token", "secret", "hash"];

    private readonly List<PendingEntry> _pending = [];
    private bool _writing;

    /// <summary>
    /// Captures what is about to change, while the change tracker still knows.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Writes the entries once the save succeeded.
    /// </summary>
    /// <remarks>
    /// After the save rather than before, for two reasons. A row that was
    /// never written must not be reported as a change; and a key the database
    /// generates only exists by now, so recording earlier would leave entries
    /// that name nothing.
    ///
    /// This runs inside whatever transaction the caller opened — for a request
    /// that is the one the entry channel began — so the change and its record
    /// commit together or not at all.
    /// </remarks>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0 || eventData.Context is null)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        var entries = _pending.ToList();
        _pending.Clear();

        // The write below is itself a SaveChanges, which would come back
        // through this interceptor.
        _writing = true;
        try
        {
            eventData.Context.Set<AuditEntry>().AddRange(
                entries.Select(pending => pending.ToAuditEntry(organization)));

            await eventData.Context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writing = false;
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? database)
    {
        if (_writing || database is null || !organization.IsEstablished)
        {
            // No established organization means the operation is one of the
            // few that legitimately runs outside one — signing in, registering
            // an organization. There is no organization to file the entry
            // under, and audit_log requires one.
            return;
        }

        foreach (var entry in database.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEntry || !IsBusinessData(entry))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "create",
                EntityState.Modified => "update",
                EntityState.Deleted => "delete",
                _ => null,
            };

            if (action is not null)
            {
                _pending.Add(new PendingEntry(entry, action, Describe(entry)));
            }
        }
    }

    /// <summary>
    /// Whether the entity is business data, which is what the log is for.
    /// </summary>
    /// <remarks>
    /// Recognized by carrying org_id, the same property that makes a row
    /// subject to the isolation policies. Accounts and sessions are excluded
    /// by the same rule: they belong to a person rather than to an
    /// organization, and there is no organization to file their changes under.
    /// </remarks>
    private static bool IsBusinessData(EntityEntry entry) =>
        entry.Metadata.FindProperty("OrgId") is not null;

    /// <summary>
    /// What changed, as JSON: the new values on a create, both sides of each
    /// altered column on an update, and the last known values on a delete.
    /// </summary>
    private static string Describe(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.GetColumnName();

            if (IsSecret(name) || IsNotWorthRecording(property))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = property.CurrentValue;
                    break;

                case EntityState.Deleted:
                    changes[name] = property.OriginalValue;
                    break;

                case EntityState.Modified when property.IsModified:
                    changes[name] = new
                    {
                        from = property.OriginalValue,
                        to = property.CurrentValue,
                    };
                    break;
            }
        }

        return JsonSerializer.Serialize(changes, SerializerOptions);
    }

    /// <summary>
    /// Values the database decides, and the key.
    /// </summary>
    /// <remarks>
    /// Read here they would be wrong rather than merely redundant: this runs
    /// before the save, so a key or a timestamp the database assigns still
    /// holds whatever placeholder stood in for it. Recording that placeholder
    /// would put an identifier in the log that names nothing, next to the
    /// real one in entity_id.
    ///
    /// Nothing is lost. The key is recorded as entity_id, and when the change
    /// happened is recorded as occurred_at.
    /// </remarks>
    private static bool IsNotWorthRecording(PropertyEntry property) =>
        property.Metadata.IsPrimaryKey()
        || property.Metadata.ValueGenerated != ValueGenerated.Never;

    /// <summary>
    /// Enum values are written by name. A role recorded as "3" asks whoever
    /// reads the log years later to know how the enum was ordered then.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static bool IsSecret(string columnName) =>
        SecretMarkers.Any(marker =>
            columnName.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A change captured before the save, resolved into an entry after it —
    /// which is when a database-generated key finally exists.
    /// </summary>
    private sealed record PendingEntry(EntityEntry Entry, string Action, string Changes)
    {
        public AuditEntry ToAuditEntry(OrganizationContext organization) => new()
        {
            OrgId = organization.RequireOrganizationId(),
            UserId = organization.UserId,
            IpAddress = organization.IpAddress,
            EntityType = Entry.Metadata.GetTableName() ?? Entry.Metadata.ClrType.Name,
            EntityId = ReadKey(),
            Action = Action,
            Changes = Changes,
        };

        /// <summary>
        /// The row's identifier, read now that the database has assigned one.
        /// </summary>
        private Guid? ReadKey() =>
            Entry.Metadata.FindPrimaryKey()?.Properties is [{ } key]
            && Entry.Property(key.Name).CurrentValue is Guid id
                ? id
                : null;
    }
}
