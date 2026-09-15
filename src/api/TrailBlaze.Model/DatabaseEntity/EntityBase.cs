namespace TrailBlaze.Model.DatabaseEntity
{
    public abstract class EntityBase
    {
        /// <summary>
        /// Application-assigned primary key. A new entity gets one at construction, so a row
        /// is identifiable before it is saved — which is what lets the audit trail record an
        /// EntityId for an insert, and what lets a caller key its own response on the id
        /// without waiting for the database to hand one back.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string? CreatedBy { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        public string? LastModifiedBy { get; set; }

        public DateTimeOffset LastModifiedOn { get; set; }

        public bool? IsDeleted { get; set; }
    }
}
