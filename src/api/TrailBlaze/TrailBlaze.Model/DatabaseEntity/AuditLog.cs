namespace TrailBlaze.Model.DatabaseEntity
{
    /// <summary>
    /// One recorded change. Deliberately not an <see cref="EntityBase"/>: the history is
    /// append-only, so soft-delete and last-modified columns would describe something that
    /// cannot happen. <see cref="Timestamp"/> and <see cref="Actor"/> are this type's own
    /// audit columns. The key is application-assigned on the same terms as the rest of the
    /// model — see <see cref="EntityBase.Id"/>.
    /// </summary>
    public sealed class AuditLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Entra Object ID of whoever made the change, or "system" for background work and
        /// "anonymous" for an unauthenticated request. Never null.
        /// </summary>
        public required string Actor { get; set; }

        /// <summary>
        /// Optional: user's display name
        /// </summary>
        public string? ActorName { get; set; }

        /// <summary>
        /// "Users", "Orders", etc. Never null.
        /// </summary>
        public required string TableName { get; set; }

        /// <summary>
        /// Primary key value (as string) of the affected row, populated for inserts as well as
        /// updates and deletes. Keys are application-assigned (see <see cref="EntityBase.Id"/>),
        /// so the value exists before the save and an insert is as traceable as anything else.
        /// Null only when the entity has no primary key, or a part of a composite one is unset.
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// The <c>EntityState</c> that produced the entry: "Added", "Modified" or "Deleted".
        /// Never null. Note that this is a soft delete's vocabulary too — clearing
        /// <c>IsDeleted</c> is an update, and is recorded as "Modified".
        /// </summary>
        public required string Action { get; set; }

        /// <summary>
        /// JSON of before-state (or null for INSERT)
        /// </summary>
        public string? OldValues { get; set; }

        /// <summary>
        /// JSON of after-state (or null for DELETE)
        /// </summary>
        public string? NewValues { get; set; }

        /// <summary>
        /// Optional: which columns actually changed
        /// </summary>
        public string? ChangedColumns { get; set; }

        /// <summary>
        /// When the action occurred
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        // Context
        /// <summary>
        /// From HTTP context
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// From HTTP context
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// For tracing across services
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
