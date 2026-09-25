namespace TrailBlaze.Repository.Test.TestSupport;

using System.Globalization;
using System.Web;

/// <summary>
/// Reads what a signed URL actually carries, so an assertion about the signature reads the URL the
/// server was handed rather than the arguments it was built from.
/// </summary>
/// <remarks>
/// A SAS is computed locally from the account key, so the arguments prove nothing on their own: a
/// wrong resource string, a wrong permission set and a wrong expiry all produce a well-formed URL.
/// Reading the value back out is what lets a test compare the URL with what was asked for.
/// </remarks>
internal static class SignedUrl
{
    /// <summary>When the signature expires — the <c>se</c> parameter, as UTC.</summary>
    public static DateTimeOffset ExpiryOf(Uri url) =>
        DateTimeOffset.Parse(
            HttpUtility.ParseQueryString(url.Query)["se"]!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    /// <summary>What the signature permits — the <c>sp</c> parameter, one letter per permission.</summary>
    public static string PermissionsOf(Uri url) => Parameter(url, "sp");

    /// <summary>What the signature is scoped to — the <c>sr</c> parameter. <c>b</c> is one blob,
    /// <c>c</c> is the whole container.</summary>
    public static string ResourceOf(Uri url) => Parameter(url, "sr");

    private static string Parameter(Uri url, string name) =>
        HttpUtility.ParseQueryString(url.Query)[name]!;
}
