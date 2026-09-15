using TrailBlaze.Interface.Infrastructure;

namespace TrailBlaze.Tests.TestSupport
{
    /// <summary>
    /// Stands in for the HTTP-backed <c>UserContextService</c>. The real one reads
    /// <c>HttpContext</c>, which the audit tests have no request for — every member here is
    /// settable so a test can describe the caller it wants.
    /// </summary>
    internal sealed class FakeUserContext : IUserContextService
    {
        /// <summary>Null models an unauthenticated caller.</summary>
        public string? EntraObjectId { get; init; }

        /// <summary>True when a request is in flight, authenticated or not.</summary>
        public bool HasActiveRequest { get; init; }

        public string? ActorName { get; init; }

        public string? IpAddress { get; init; }

        public string? UserAgent { get; init; }

        public string? CorrelationId { get; init; }
    }
}
