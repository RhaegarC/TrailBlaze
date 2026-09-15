using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;

namespace TrailBlaze.Repository
{
    internal sealed class AuditSaveChangesInterceptor(IUserContextService userContextService) : SaveChangesInterceptor
    {
        /// <summary>Attribution for work with no request behind it — startup, background jobs.</summary>
        private const string SystemActor = "system";

        /// <summary>Attribution for a request that arrived without an authenticated user.</summary>
        private const string AnonymousActor = "anonymous";

        private readonly IUserContextService _currentUser = userContextService;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = false
        };

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Apply(eventData.Context);
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Apply(eventData.Context);
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Apply(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var actor = ResolveActor();

            foreach (var entry in context.ChangeTracker.Entries<EntityBase>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedOn = now;
                        entry.Entity.CreatedBy ??= actor;
                        entry.Entity.IsDeleted = false;
                        break;
                    case EntityState.Modified:
                        entry.Entity.LastModifiedOn = now;
                        entry.Entity.LastModifiedBy = actor;
                        break;
                }
            }
        }

        /// <summary>
        /// Captures the audit entries and adds them to the same context, so they are written in
        /// the same transaction as the change they describe. Both save paths call this: a
        /// synchronous <c>SaveChanges()</c> would otherwise bypass auditing in silence, which is
        /// the kind of gap nobody notices until they need the history.
        /// </summary>
        private void Capture(DbContext? context)
        {
            if (context is not TrailBlazeContext dbContext)
            {
                return;
            }

            dbContext.AuditLogs.AddRange(CaptureAuditLogs(context));
        }

        private List<AuditLog> CaptureAuditLogs(DbContext? context)
        {
            if (context == null) return new List<AuditLog>();

            var logs = new List<AuditLog>();
            var now = DateTimeOffset.UtcNow;
            var actor = ResolveActor();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog) continue;

                if (entry.State == EntityState.Unchanged) continue;

                // The mapped table name is absent for a type that has no table — the column is
                // NOT NULL, so fall back to the CLR type name rather than writing null.
                var tableName = entry.Metadata.GetTableName()
                    ?? entry.Metadata.GetDefaultTableName()
                    ?? entry.Metadata.ClrType.Name;
                var entityId = GetPrimaryKeyValue(entry);

                var oldValues = entry.State == EntityState.Added
                    ? null
                    : SerializeEntity(entry, isOriginal: true);

                var newValues = entry.State == EntityState.Deleted
                    ? null
                    : SerializeEntity(entry, isOriginal: false);

                var log = new AuditLog
                {
                    TableName = tableName,
                    EntityId = entityId,
                    Action = entry.State.ToString(),
                    OldValues = oldValues,
                    NewValues = newValues,
                    ChangedColumns = GetChangedColumns(entry),
                    Actor = actor,
                    ActorName = _currentUser.ActorName,
                    Timestamp = now,
                    IpAddress = _currentUser.IpAddress,
                    UserAgent = _currentUser.UserAgent,
                    CorrelationId = _currentUser.CorrelationId
                };

                logs.Add(log);
            }

            return logs;
        }

        /// <summary>
        /// <see cref="AuditLog.Actor"/> is NOT NULL, so an unidentified caller still needs a
        /// value. A request that arrived unauthenticated and work that ran with no request at
        /// all are different problems — the first is a missing token, the second is expected —
        /// so they get different actors rather than one shared "unknown".
        /// </summary>
        private string ResolveActor()
        {
            string? actor = _currentUser.EntraObjectId;

            if (!string.IsNullOrWhiteSpace(actor))
            {
                return actor;
            }

            return _currentUser.HasActiveRequest ? AnonymousActor : SystemActor;
        }

        /// <summary>
        /// The affected row's key, read before the save. That is only possible because keys are
        /// application-assigned (<see cref="EntityBase.Id"/>), so an insert is as traceable as
        /// an update — which is the opposite of what a database-generated key allows, since
        /// there is nothing to read until after the save has already happened.
        /// </summary>
        private static string? GetPrimaryKeyValue(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key == null)
            {
                return null;
            }

            var values = key.Properties
                .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
                .ToArray();

            // If any part is unset, report no key at all rather than a partial one. Joining
            // whatever happened to be present would write "abc" for a two-part key whose
            // second half is missing — a value that reads like a real id and is not one.
            return values.Any(string.IsNullOrEmpty) ? null : string.Join("-", values);
        }

        /// <remarks>
        /// <c>internal</c> rather than <c>private</c> so the test project can call it directly.
        /// The type is itself internal, so this does not widen the assembly's public surface.
        /// </remarks>
        internal string? SerializeEntity(EntityEntry entry, bool isOriginal)
        {
            // Clone the entity's properties to a dictionary
            var properties = entry.Metadata.GetProperties()
                .Where(p => !p.IsShadowProperty()) // Skip EF shadow properties
                .Where(p => !IsNotAudited(p))      // Skip properties opted out of the snapshot
                .ToDictionary(
                    p => p.Name,
                    p => isOriginal
                        ? entry.Property(p.Name).OriginalValue
                        : entry.Property(p.Name).CurrentValue
                );

            return JsonSerializer.Serialize(properties, _jsonOptions);
        }

        /// <summary>
        /// <see cref="NotAuditedAttribute"/> sits on the CLR property, and an EF property only
        /// has a <see cref="IProperty.PropertyInfo"/> when it is mapped to one. A shadow
        /// property has none, so it cannot be opted out this way — which is fine, since a
        /// shadow property has no CLR value to leak.
        /// </summary>
        private static bool IsNotAudited(IProperty property) =>
            property.PropertyInfo?.GetCustomAttribute<NotAuditedAttribute>(inherit: true) is not null;

        private string? GetChangedColumns(EntityEntry entry)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Deleted)
                return null;

            var changed = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToList();

            return changed.Any() ? string.Join(", ", changed) : null;
        }
    }
}