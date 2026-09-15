using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Api;
using TrailBlaze.Model;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// The composition root is startup behaviour with no endpoint to call, so these tests
    /// drive <c>ServiceExt</c> directly. They cover the two guards that decide whether a
    /// misconfigured deployment fails loudly or runs with a hole in it.
    /// </summary>
    public class StartupWiringTests
    {
        private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
                .Build();

        // ---- CORS (IM-02) ----------------------------------------------------------

        /// <summary>
        /// A missing origin list used to be an <c>ArgumentNullException</c> naming a local
        /// variable. It is a configuration error, so it should say which setting is missing.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CORS_refuses_to_start_without_origins(string? origins)
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AllowCORS(Configuration((Constant.ConfigKey.AllowedOrigins, origins))));

            Assert.Contains(Constant.ConfigKey.AllowedOrigins, exception.Message);
        }

        /// <summary>
        /// The origins are comma-separated. Reading them with the wrong separator silently
        /// yields one origin that matches nothing — CORS then fails at runtime, in the browser,
        /// far from the mistake.
        /// </summary>
        [Fact]
        public async Task CORS_splits_origins_on_comma_and_trims_them()
        {
            var services = new ServiceCollection();
            services.AllowCORS(Configuration((Constant.ConfigKey.AllowedOrigins,
                " https://a.example , https://b.example ")));

            await using var provider = services.BuildServiceProvider();
            var policy = await provider.GetRequiredService<ICorsPolicyProvider>()
                .GetPolicyAsync(new DefaultHttpContext(), Constant.App.CORSPolicyName);

            Assert.NotNull(policy);
            Assert.Equal(["https://a.example", "https://b.example"], policy.Origins);
        }

        // ---- Entra ID authentication (IM-05) ---------------------------------------

        /// <summary>
        /// With no tenant configured the app must still start — that is the whole point of the
        /// lenient path — but it must start with no authentication scheme rather than with a
        /// half-configured one. <c>Program</c> logs a warning for this case.
        /// </summary>
        [Fact]
        public async Task No_tenant_means_no_authentication_scheme()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEntraAuthentication(Configuration());

            await using var provider = services.BuildServiceProvider();
            var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>()
                .GetDefaultAuthenticateSchemeAsync();

            Assert.Null(scheme);
        }

        /// <summary>
        /// Half-configured is the case worth pinning: a tenant with no audience would otherwise
        /// produce a JWT bearer handler that rejects every token, which reads as a broken token
        /// rather than a missing setting.
        /// </summary>
        [Fact]
        public async Task A_tenant_without_an_audience_still_registers_no_scheme()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEntraAuthentication(Configuration((Constant.ConfigKey.TenantId, "tenant-1")));

            await using var provider = services.BuildServiceProvider();
            var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>()
                .GetDefaultAuthenticateSchemeAsync();

            Assert.Null(scheme);
        }

        [Fact]
        public async Task A_configured_tenant_and_audience_select_bearer_authentication()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEntraAuthentication(Configuration(
                (Constant.ConfigKey.TenantId, "tenant-1"),
                (Constant.ConfigKey.Audience, "api://backend")));

            await using var provider = services.BuildServiceProvider();
            var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>()
                .GetDefaultAuthenticateSchemeAsync();

            Assert.NotNull(scheme);
            Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme.Name);
        }

        /// <summary>
        /// The authority is the v2.0 metadata endpoint, and it is built from the tenant id
        /// rather than hard-coded. Validating the issuer is the metadata's job here, so a
        /// wrong authority is the one mistake that would quietly accept tokens from another
        /// tenant.
        /// </summary>
        [Fact]
        public async Task The_authority_is_the_v2_endpoint_for_the_configured_tenant()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEntraAuthentication(Configuration(
                (Constant.ConfigKey.TenantId, "tenant-1"),
                (Constant.ConfigKey.Audience, "api://backend")));

            await using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<
                Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>();

            Assert.Equal(
                "https://login.microsoftonline.com/tenant-1/v2.0",
                options.Get(JwtBearerDefaults.AuthenticationScheme).Authority);
        }
    }
}
