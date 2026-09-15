namespace TrailBlaze.Model
{
    public static class Constant
    {
        public static class App
        {
            public const string CORSPolicyName = "AllowSpecificOrigin";
            public const string HealthCheckUrl = "/health";
        }

        public static class Message
        {
            public const string NoAllowedOrigins = "Allowed origins not configured. Set the 'AllowedOrigins' configuration value (comma-separated).";
        }

        public static class ConfigKey
        {
            public const string DBCon = "DbConnection";

            public const string TenantId = "TenantId";

            public const string Audience = "Audience";

            public const string AllowedOrigins = "AllowedOrigins";
        }
    }
}
