using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OnboardFlow.Api.Tests;

public sealed class ApiSafetyTests
{
    private static WebApplicationFactory<Program> Factory(int requestsPerMinute = 240) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OnboardFlow"] = "Host=127.0.0.1;Database=unused;Username=unused",
                    ["ApiLimits:RequestsPerMinute"] = requestsPerMinute.ToString()
                }));
        });

    [Fact]
    public async Task Excess_requests_are_rejected_without_blocking_static_assets()
    {
        await using var factory = Factory(2);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
        var rejected = await client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.True(rejected.Headers.CacheControl?.NoStore);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/favicon.svg")).StatusCode);
    }

    [Fact]
    public async Task Oversized_requests_are_rejected_before_session_or_database_work()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var body = new StringContent(new string('x', 32 * 1024));
        var response = await client.PostAsync("/api/session", body);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Api_errors_are_uncached_and_have_basic_browser_protections()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("StackTrace", await response.Content.ReadAsStringAsync());
    }
}
