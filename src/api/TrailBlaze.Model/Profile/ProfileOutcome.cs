namespace TrailBlaze.Model.Profile;

/// <summary>
/// The result of a profile operation: the caller's profile, the reasons the input was
/// rejected, or the fact that the request named no caller to act on.
/// </summary>
/// <remarks>
/// <para>
/// A result type rather than exceptions, because every one of these outcomes is an ordinary
/// answer rather than a fault: a user typing a blank display name is not an error condition,
/// and an authenticated token that carries no object id is a configuration fact about the
/// tenant rather than a bug. Throwing would also push the mapping to HTTP up into the service
/// layer, which knows nothing about status codes.
/// </para>
/// <para>
/// <b>Three states, and exactly one of them is set at a time.</b> <see cref="Succeeded"/>
/// distinguishes the first from the other two, and <see cref="HasCaller"/> separates those —
/// so no caller has to inspect a null field to work out which it is holding.
/// </para>
/// <para>
/// A class rather than a record, unlike the two data shapes beside it: it carries a dictionary
/// of errors, and a record's generated equality would compare that by reference, so two
/// outcomes describing the same rejection would compare unequal. Value semantics are the one
/// thing this type cannot honestly claim.
/// </para>
/// </remarks>
public sealed class ProfileOutcome
{
    private ProfileOutcome()
    {
    }

    /// <summary>The caller's profile after the operation. Set only when it succeeded.</summary>
    public UserProfileResponse? Profile { get; private init; }

    /// <summary>
    /// Why the input was rejected, keyed by field as
    /// <c>ValidationProblemDetails.Errors</c> is — so the controller hands it over without
    /// reshaping it. Set only when the input was rejected.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>False when the request carried no Entra object id, so there is no row to read
    /// or write. The caller must be answered with a 401.</summary>
    public bool HasCaller { get; private init; } = true;

    /// <summary>True when <see cref="Profile"/> is the result and nothing was rejected.</summary>
    public bool Succeeded => Profile is not null;

    /// <summary>The operation completed. <paramref name="profile"/> is the row as it now
    /// stands, re-read after the write rather than echoed from the request.</summary>
    public static ProfileOutcome Completed(UserProfileResponse profile) =>
        new() { Profile = profile };

    /// <summary>The input was rejected. <paramref name="field"/> names the request property
    /// the message belongs to, so the client can mark the right control.</summary>
    public static ProfileOutcome Rejected(string field, string message) =>
        new() { Errors = new Dictionary<string, string[]> { [field] = [message] } };

    /// <summary>Several fields were rejected at once — a profile update reports every problem
    /// it found rather than only the first, so a client does not need one round trip per
    /// mistake.</summary>
    public static ProfileOutcome Rejected(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Errors = errors };

    /// <summary>The request named no caller.</summary>
    public static ProfileOutcome NoCaller() => new() { HasCaller = false };
}
