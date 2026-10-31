using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class FoundationFactory(string connection, TestClock clock) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OnboardFlow"] = connection,
                // This shared test host runs many independent visitors in seconds.
                // A separate production-policy test exercises actual request rejection.
                ["ApiLimits:RequestsPerMinute"] = "10000"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
    }
}

public sealed class FoundationFixture : IAsyncLifetime
{
    public TestClock Clock { get; } = new();
    public FoundationFactory Factory { get; private set; } = null!;
    private string adminConnection = "";
    private readonly string database = "onboardflow_test_" + Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("ONBOARDFLOW_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Use npm run verify to configure disposable PostgreSQL tests."));
        if (connection.Host != "127.0.0.1" || connection.Port != 54329 || connection.Username != "onboardflow")
            throw new InvalidOperationException("Tests require this project's loopback-only PostgreSQL container.");
        connection.Database = "postgres";
        connection.Pooling = false;
        adminConnection = connection.ConnectionString;
        await AdminSql($"CREATE DATABASE {database}");
        connection.Database = database;
        Factory = new FoundationFactory(connection.ConnectionString, Clock);
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FoundationDb>().Database.MigrateAsync();
    }

    public HttpClient Client(bool cookies = true) => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = cookies,
        AllowAutoRedirect = false
    });

    // Existing project records remain supported; only legacy regression fixtures seed them now.
    public async Task SeedLegacyProjects(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        db.Projects.AddRange(new[] { ("OF-1042", "Website redesign"), ("OF-1043", "Website refresh") }
            .Select(item => new Project { Id = Guid.NewGuid(), SandboxSessionId = sessionId,
                Reference = item.Item1, Name = item.Item2, ClientName = "Juniper Studio", ClientContact = "Maya Ellis",
                Owner = "Alex Morgan", Service = "Website design", SignedPages = ["Home", "About", "Services", "Projects", "Contact"] }));
        await db.SaveChangesAsync();
    }

    private async Task AdminSql(string sql)
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        // Name is generated here, never taken from the development connection or a request.
        if (!string.IsNullOrEmpty(adminConnection)) await AdminSql($"DROP DATABASE IF EXISTS {database} WITH (FORCE)");
    }
}
