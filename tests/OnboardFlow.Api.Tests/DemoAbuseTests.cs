using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class DemoAbuseTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    // Test-only stand-in for the already-tested verified gateway boundary.
    private sealed class CallerFixture : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((http, proceed) =>
            {
                if (http.Request.Headers["X-Test-Network"].FirstOrDefault() is { Length: 64 } network)
                    http.Features.Set(new VerifiedCaller(network));
                return proceed(http);
            });
            next(app);
        };
    }
    private sealed class FailWrite : SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result,
            CancellationToken cancellationToken = default) => Fail
            ? throw new InvalidOperationException("Injected failure before commit") : ValueTask.FromResult(result);
    }
    private sealed class HoldConnection : DbConnectionInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return result;
        }
    }
    private async Task<(HttpClient Client, string Csrf)> Visitor(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, char network)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Network", new string(network, 64));
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        (await Post(client, csrf, "/api/session", new { })).EnsureSuccessStatusCode();
        return (client, csrf);
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string csrf, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return client.SendAsync(request);
    }
    private static async Task<AgentCommand> Command(HttpClient client) => new(Guid.NewGuid(),
        (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!.Version);

    [Fact]
    public async Task Fresh_cookies_share_a_durable_network_allowance_and_replays_do_not_consume_it()
    {
        var model = new IntakeTests.FakeModel { Failure = "timeout" };
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Abuse:PublicStartsPerNetworkPerDay"] = "2" }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter, CallerFixture>();
                services.RemoveAll<IIntakeModelClient>(); services.AddSingleton<IIntakeModelClient>(model);
            });
        });
        var first = await Visitor(factory, 'a'); using var firstClient = first.Client;
        var command = await Command(firstClient);
        (await Post(firstClient, first.Csrf, "/api/intake/agent", command)).EnsureSuccessStatusCode();
        (await Post(firstClient, first.Csrf, "/api/intake/agent", command)).EnsureSuccessStatusCode();
        Assert.Equal(1, model.Calls);
        var second = await Visitor(factory, 'a'); using var secondClient = second.Client;
        (await Post(secondClient, second.Csrf, "/api/intake/agent", await Command(secondClient))).EnsureSuccessStatusCode();
        var third = await Visitor(factory, 'a'); using var thirdClient = third.Client;
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(thirdClient, third.Csrf, "/api/intake/agent", await Command(thirdClient))).StatusCode);
        Assert.Equal(2, model.Calls);
        var other = await Visitor(factory, 'b'); using var otherClient = other.Client;
        (await Post(otherClient, other.Csrf, "/api/intake/agent", await Command(otherClient))).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        Assert.Equal(2, (await db.NetworkRunUsage.SingleAsync(x => x.Network == new string('a', 64))).Starts);
        Assert.Equal(1, (await db.NetworkRunUsage.SingleAsync(x => x.Network == new string('b', 64))).Starts);
        // Owner exemption bypasses visitor counts, while the existing shared model budget is unchanged.
        var token = (await Post(thirdClient, third.Csrf, "/api/session/reset", new { })).Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith(SandboxEndpoints.CookieName + "=")).Split(';')[0].Split('=', 2)[1];
        var snapshot = await Command(thirdClient);
        await scope.ServiceProvider.GetRequiredService<IntakeAgentService>().Run(token, snapshot, CancellationToken.None, true, new string('a', 64));
        Assert.Equal(2, (await db.NetworkRunUsage.AsNoTracking().SingleAsync(x => x.Network == new string('a', 64))).Starts);
    }

    [Fact]
    public async Task Simultaneous_workspaces_cannot_overrun_the_network_counter()
    {
        var model = new IntakeTests.FakeModel { Failure = "timeout" };
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Abuse:PublicStartsPerNetworkPerDay"] = "1" }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter, CallerFixture>();
                services.RemoveAll<IIntakeModelClient>(); services.AddSingleton<IIntakeModelClient>(model);
            });
        });
        var a = await Visitor(factory, 'c'); using var ac = a.Client;
        var b = await Visitor(factory, 'c'); using var bc = b.Client;
        var ca = await Command(ac); var cb = await Command(bc);
        var responses = await Task.WhenAll(Post(ac, a.Csrf, "/api/intake/agent", ca), Post(bc, b.Csrf, "/api/intake/agent", cb));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Counter_and_request_roll_back_together_and_legacy_reset_keeps_run_usage()
    {
        var failure = new FailWrite(); var model = new IntakeTests.FakeModel { Failure = "timeout" };
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, CallerFixture>();
            services.RemoveAll<IIntakeModelClient>(); services.AddSingleton<IIntakeModelClient>(model);
            services.AddDbContext<FoundationDb>(options => options.AddInterceptors(failure));
        }));
        var visitor = await Visitor(factory, 'd'); using var client = visitor.Client;
        var command = await Command(client); failure.Fail = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post(client, visitor.Csrf, "/api/intake/agent", command)).StatusCode);
        failure.Fail = false;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
            Assert.False(await db.NetworkRunUsage.AnyAsync(x => x.Network == new string('d', 64)));
            var session = (await client.GetFromJsonAsync<SessionView>("/api/session"))!;
            var row = await db.ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id);
            var state = JsonSerializer.Deserialize<IntakeState>(row.StateJson, IntakeSpecification.Json)!;
            Assert.Equal(0, state.RunsStarted); state.RunsStarted = 3;
            row.StateJson = JsonSerializer.Serialize(state, IntakeSpecification.Json); await db.SaveChangesAsync();
        }
        (await Post(client, visitor.Csrf, "/api/session/reset", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, visitor.Csrf, "/api/intake/agent", await Command(client))).StatusCode);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task One_caller_is_throttled_without_using_another_callers_budget()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["ApiLimits:CallerRequestsPerMinute"] = "2" }));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, CallerFixture>());
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Network", new string('e', 64));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/unknown")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Network"); client.DefaultRequestHeaders.Add("X-Test-Network", new string('f', 64));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
    }

    [Fact]
    public async Task Readiness_has_one_database_slot_and_times_out_while_native_liveness_stays_available()
    {
        var hold = new HoldConnection();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FoundationDb>(options => options.AddInterceptors(hold))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var pending = client.GetAsync("/health/ready");
        await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/csrf")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await pending.WaitAsync(TimeSpan.FromSeconds(8))).StatusCode);
    }
}
