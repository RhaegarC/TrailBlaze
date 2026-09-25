namespace TrailBlaze.Model.Media;

/// <summary>
/// A time-limited address for one item's bytes, and the moment it stops working.
/// </summary>
/// <remarks>
/// The expiry is part of the payload rather than a detail a client infers, because a SAS is a bearer
/// token: whoever holds the URL holds the bytes until it lapses, so a client that cannot tell when
/// that is cannot cache the link safely or refresh it in time.
/// </remarks>
public sealed record MediaUrlResponse
{
    /// <summary>The signed URL. A credential, so it is answered to the caller and never logged.</summary>
    public required string Url { get; init; }

    /// <summary>When <see cref="Url"/> stops being accepted, as a UTC instant.</summary>
    public required DateTimeOffset ExpiresOnUtc { get; init; }
}
