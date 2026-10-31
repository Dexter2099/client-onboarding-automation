using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace OnboardFlow.Api.Tests;

public sealed class PostgresTransportTests
{
    [Theory]
    [InlineData(false, GssEncryptionMode.Prefer, GssEncryptionMode.Disable)]
    [InlineData(true, GssEncryptionMode.Prefer, GssEncryptionMode.Disable)]
    [InlineData(true, GssEncryptionMode.Require, GssEncryptionMode.Require)]
    [InlineData(true, GssEncryptionMode.Disable, GssEncryptionMode.Disable)]
    public async Task Application_and_budget_connections_preserve_tls_and_credentials_without_optional_gss(
        bool dedicatedLedger, GssEncryptionMode configuredGss, GssEncryptionMode expectedGss)
    {
        var primary = Connection("fixture-main", SslMode.VerifyFull, configuredGss);
        var ledger = dedicatedLedger ? Connection("fixture-ledger", SslMode.Require, configuredGss) : primary;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OnboardFlow"] = primary.ConnectionString,
                    ["Intake:SpendConnectionString"] = dedicatedLedger ? ledger.ConnectionString : null,
                    ["VerifiedEdge:Enabled"] = "false"
                })));
        using var scope = factory.Services.CreateScope();
        AssertSettings(primary, scope.ServiceProvider.GetRequiredService<FoundationDb>(), expectedGss);
        AssertSettings(ledger, scope.ServiceProvider.GetRequiredKeyedService<FoundationDb>("intake-spend"), expectedGss);
    }

    private static NpgsqlConnectionStringBuilder Connection(string database, SslMode sslMode, GssEncryptionMode gss)
        => new()
        {
            Host = "database.fixture.invalid",
            Port = 5433,
            Database = database,
            Username = database + "-user",
            Password = "fixture;quoted=password",
            SslMode = sslMode,
            RootCertificate = "fixture-root.pem",
            CheckCertificateRevocation = true,
            ChannelBinding = ChannelBinding.Require,
            Timeout = 7,
            GssEncryptionMode = gss
        };

    private static void AssertSettings(NpgsqlConnectionStringBuilder expected, FoundationDb database,
        GssEncryptionMode expectedGss)
    {
        // Resolve the real DI registrations, but never open a database connection or log its credentials.
        var actual = new NpgsqlConnectionStringBuilder(database.Database.GetDbConnection().ConnectionString);
        Assert.Equal(expectedGss, actual.GssEncryptionMode);
        foreach (var key in expected.Keys.Cast<string>().Where(key => key != "GSS Encryption Mode"))
            Assert.Equal(expected[key], actual[key]);
    }
}
