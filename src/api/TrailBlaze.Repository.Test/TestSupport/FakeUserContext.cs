namespace TrailBlaze.Repository.Test.TestSupport
{
    using TrailBlaze.Interface.Infrastructure;

    /// <summary>
    /// Stands in for the HTTP-backed <see cref="IUserContextService"/> so a test can set the
    /// caller it wants to reason about.
    /// </summary>
    /// <remarks>
    /// The interface is get-only on purpose — the real values describe a request in flight and
    /// nothing outside the abstraction should rewrite them — but a fake has to be settable, so
    /// the properties are read/write here. That is the point of the seam: the repository tier
    /// proves what it does *given* a caller, and the real token pipeline behind that caller is
    /// feature 11's subject, not this tier's.
    /// </remarks>
    public sealed class FakeUserContext : IUserContextService
    {
        /// <inheritdoc/>
        public string? EntraObjectId { get; set; }

        /// <inheritdoc/>
        public bool HasActiveRequest { get; set; }

        /// <inheritdoc/>
        public string? ActorName { get; set; }

        /// <inheritdoc/>
        public string? Email { get; set; }

        /// <inheritdoc/>
        public string? IpAddress { get; set; }

        /// <inheritdoc/>
        public string? UserAgent { get; set; }

        /// <inheritdoc/>
        public string? CorrelationId { get; set; }

        /// <summary>A validated caller. The audit trail should name them by object id.</summary>
        public static FakeUserContext Authenticated(string entraObjectId) => new()
        {
            HasActiveRequest = true,
            EntraObjectId = entraObjectId,
        };

        /// <summary>A request that arrived without a user. Distinct from
        /// <see cref="NoRequest"/>: this one is a missing token, that one is expected.</summary>
        public static FakeUserContext AnonymousRequest() => new()
        {
            HasActiveRequest = true,
        };

        /// <summary>No request at all — startup, a background job.</summary>
        public static FakeUserContext NoRequest() => new()
        {
            HasActiveRequest = false,
        };
    }
}
