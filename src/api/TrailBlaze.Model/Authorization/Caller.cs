namespace TrailBlaze.Model.Authorization;

/// <summary>
/// Who is acting, as the permission rule sees them: an object id, and whether the <c>users</c> row
/// behind it carries the elevated role.
/// </summary>
/// <remarks>
/// Built by the authorization service from that row and from nothing else. There is deliberately no
/// claim it can be assembled out of, which is what keeps Decision #9 true: a token that looks
/// administrative is an ordinary caller until the row says otherwise, and the row is the only thing
/// that can be set by hand.
/// </remarks>
public sealed record Caller(string? Id, bool IsAdmin)
{
    /// <summary>No token named anyone: no id, no privilege, and nothing may widen it.</summary>
    public static readonly Caller Anonymous = new(null, false);

    /// <summary>Whether a token named anyone at all — the difference between 401 and 403.</summary>
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(Id);
}
