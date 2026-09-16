namespace TrailBlaze.Api;

using System.Security.Claims;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Model;

/// <summary>
/// Reads the acting user and the request context straight from <see cref="HttpContext"/>.
/// Every member is computed on access rather than captured by the constructor: the service
/// is scoped, so it describes one request for its whole lifetime, and resolving it outside
/// a request yields nulls instead of freezing whatever happened to be current at the moment
/// it was built.
/// </summary>
public class UserContextService(IHttpContextAccessor httpContextAccessor) : IUserContextService
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private HttpContext? Context => httpContextAccessor.HttpContext;

    /// <inheritdoc/>
    public bool HasActiveRequest => Context is not null;

    /// <inheritdoc/>
    public string? EntraObjectId =>
        AuthenticatedUser?.FindFirst(Constant.Claim.ObjectId)?.Value
        ?? AuthenticatedUser?.FindFirst(Constant.Claim.ObjectIdSchema)?.Value;

    /// <inheritdoc/>
    public string? ActorName =>
        AuthenticatedUser?.FindFirst(Constant.Claim.Name)?.Value
        ?? AuthenticatedUser?.FindFirst(Constant.Claim.PreferredUsername)?.Value;

    /// <inheritdoc/>
    /// <remarks>
    /// Deliberately no fallback to <c>preferred_username</c>, even though it often holds the
    /// same address: it is already <see cref="ActorName"/>'s fallback, and it is not required
    /// to be an address at all — a tenant can configure it to a phone number or an opaque
    /// identifier. Copying it into an email column would store a value the column's name
    /// asserts to be something it may not be. A null here is honest; the wrong address is
    /// not.
    /// </remarks>
    public string? Email =>
        AuthenticatedUser?.FindFirst(Constant.Claim.Email)?.Value
        ?? AuthenticatedUser?.FindFirst(Constant.Claim.EmailSchema)?.Value;

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
