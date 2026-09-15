using System.Security.Claims;
using TrailBlaze.Interface.Infrastructure;

namespace TrailBlaze.Api
{
    /// <summary>
    /// Reads the acting user and the request context straight from <see cref="HttpContext"/>.
    /// Every member is computed on access rather than captured by the constructor: the service
    /// is scoped, so it describes one request for its whole lifetime, and resolving it outside
    /// a request yields nulls instead of freezing whatever happened to be current at the moment
    /// it was built.
    /// </summary>
    public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
    {
        /// <summary>Entra ID's short claim for the object id.</summary>
        private const string ObjectIdClaim = "oid";

        /// <summary>
        /// The long-form claim older tokens carry for the same value. Both are read, so
        /// whichever the token happens to use is picked up.
        /// </summary>
        private const string ObjectIdSchemaClaim =
            "http://schemas.microsoft.com/identity/claims/objectidentifier";

        private const string CorrelationIdHeader = "X-Correlation-Id";

        private HttpContext? Context => httpContextAccessor.HttpContext;

        /// <inheritdoc/>
        public bool HasActiveRequest => Context is not null;

        /// <inheritdoc/>
        public string? EntraObjectId =>
            AuthenticatedUser?.FindFirst(ObjectIdClaim)?.Value
            ?? AuthenticatedUser?.FindFirst(ObjectIdSchemaClaim)?.Value;

        /// <inheritdoc/>
        public string? ActorName =>
            AuthenticatedUser?.FindFirst("name")?.Value
            ?? AuthenticatedUser?.FindFirst("preferred_username")?.Value;

        /// <inheritdoc/>
        public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

        /// <inheritdoc/>
        public string? UserAgent => Context?.Request.Headers.UserAgent.FirstOrDefault();

        /// <inheritdoc/>
        public string? CorrelationId =>
            Context?.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Context?.TraceIdentifier;

        /// <summary>
        /// The caller, but only when the token behind them was actually validated. An
        /// unauthenticated principal carries no claims this app has reason to trust, and the
        /// audit trail attributes such requests to "anonymous" rather than to a claimed id.
        /// </summary>
        private ClaimsPrincipal? AuthenticatedUser
        {
            get
            {
                HttpContext? context = Context;

                if (context is null || context.User.Identity?.IsAuthenticated != true)
                {
                    return null;
                }

                return context.User;
            }
        }
    }
}
