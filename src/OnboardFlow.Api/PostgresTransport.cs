using Npgsql;

namespace OnboardFlow.Api;

internal static class PostgresTransport
{
    public static string WithoutOptionalGss(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        // Neon uses the configured TLS/password connection. Npgsql 10's optional GSS probe emits
        // native missing-library errors on Alpine; preserve TLS settings and explicit GSS Require.
        if (settings.GssEncryptionMode == GssEncryptionMode.Prefer)
            settings.GssEncryptionMode = GssEncryptionMode.Disable;
        return settings.ConnectionString;
    }
}
