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
    /// <remarks>
    /// The two admin keys are here for a sharper reason than the connection strings. Absent
    /// admin configuration is a system with no administrator, and that is not a state the host
    /// may reach by starting and hoping: it is unadministrable, nothing later in the run
    /// reports it, and the deployment that produced it looked healthy.
    /// </remarks>
    [Theory]
    [InlineData("DbConnection", "DbConnection")]
    [InlineData("BlobConnection", "BlobConnection")]
    [InlineData("AdminObjectId", "AdminObjectId")]
    [InlineData("AdminDisplayName", "AdminDisplayName")]
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
    /// An admin display name longer than the column it is going into stops the host too.
    /// </summary>
    /// <remarks>
    /// The alternative is worse than it looks. Seeding runs in a hosted service whose failures
    /// are logged rather than fatal — an unreachable database must not crash-loop a replica —
    /// so an over-long name would be swallowed by that same tolerance, and the operator would
    /// get a booted, healthy-looking, adminless system with one error line in the log. Rejecting
    /// it where every other configuration mistake is rejected keeps that path for the one case
    /// it was built for.
    /// </remarks>
    [Fact]
    public void Startup_fails_and_names_the_setting_when_the_admin_name_exceeds_its_column()
    {
        using var factory = new TrailBlazeApiFactory(new()
        {
            ["AdminDisplayName"] = new string('x', Constant.UserProfile.DisplayNameLength + 1),
        });

        Exception failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("AdminDisplayName", FailureMessages(failure));
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
