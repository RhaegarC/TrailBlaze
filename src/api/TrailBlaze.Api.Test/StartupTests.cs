namespace TrailBlaze.Api.Test;

using System.Net;
using TrailBlaze.Model;

/// <summary>
/// How the host behaves at startup and at its one public endpoint.
/// </summary>
public sealed class StartupTests
{
    /// <summary>
    /// Liveness has to answer without a token. This client sends no <c>Authorization</c>
    /// header at all, which is the point: every endpoint is anonymous until it is
    /// deliberately protected, and this one is deliberately public.
    /// </summary>
    [Fact]
    public async Task Health_answers_200_with_no_authorization_header()
    {
        await using var factory = new TrailBlazeApiFactory();
        using HttpClient client = factory.CreateClient();

        Assert.Null(client.DefaultRequestHeaders.Authorization);

        HttpResponseMessage response = await client.GetAsync(Constant.App.HealthCheckUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A missing required setting stops the host, naming the key. The alternative — starting
    /// half-configured and failing on the first request that touches the database — turns a
    /// deployment mistake into a runtime error nowhere near its cause.
    /// </summary>
    [Theory]
    [InlineData("DbConnection", "DbConnection")]
    [InlineData("BlobConnection", "BlobConnection")]
    public void Startup_fails_and_names_the_setting_when_required_configuration_is_absent(
        string missingKey,
        string expectedInMessage)
    {
        // Blanked, not omitted: appsettings.json declares every key with an empty default,
        // so "not configured" is the empty string rather than an absent key.
        using var factory = new TrailBlazeApiFactory(new() { [missingKey] = "" });

        Exception failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(expectedInMessage, FailureMessages(failure));
    }

    /// <summary>
    /// Walks the chain: the host wraps a startup failure from the composition root, so the
    /// message that names the setting is rarely on the outermost exception.
    /// </summary>
    private static string FailureMessages(Exception exception)
    {
        var messages = new List<string>();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
