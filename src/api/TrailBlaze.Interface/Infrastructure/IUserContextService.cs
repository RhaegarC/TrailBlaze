namespace TrailBlaze.Interface.Infrastructure
{
    /// <summary>
    /// Who is acting, and from where. Every member is read-only: the values describe the
    /// request in flight, so nothing outside this abstraction should be able to rewrite them.
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Entra Object ID for the user. Null when the request is unauthenticated, or when
        /// there is no request in flight at all.
        /// </summary>
        string? EntraObjectId { get; }

        /// <summary>
        /// True when this service was resolved inside an HTTP request. False for startup and
        /// background work, which have no request to attribute a change to.
        /// </summary>
        bool HasActiveRequest { get; }

        /// <summary>
        /// Optional: user's display name
        /// </summary>
        string? ActorName { get; }

        // Context
        /// <summary>
        /// From HTTP context
        /// </summary>
        string? IpAddress { get; }

        /// <summary>
        /// From HTTP context
        /// </summary>
        string? UserAgent { get; }

        /// <summary>
        /// For tracing across services
        /// </summary>
        string? CorrelationId { get; }

    }
}
