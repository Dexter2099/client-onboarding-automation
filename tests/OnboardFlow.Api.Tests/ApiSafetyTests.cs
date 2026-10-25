using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OnboardFlow.Api.Tests;

[Trait("Category", "Integration")]
public sealed class ApiSafetyTests
{
    private static WebApplicationFactory<Program> Factory(int requestsPerMinute = 240, string environment = "Production") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
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

    [Theory]
    [InlineData("/")]
    [InlineData("/favicon.svg")]
    [InlineData("/api/unknown")]
    public async Task Documents_assets_and_errors_enforce_browser_policy_and_https_pinning(string path)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://demo.example") });
        var response = await client.GetAsync(path);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'none'", policy);
        foreach (var directive in new[] { "script", "style", "img", "font", "connect" })
            Assert.Contains($"{directive}-src 'self'", policy);
        foreach (var directive in new[] { "script-src-attr", "style-src-attr", "base-uri", "object-src", "frame-ancestors", "form-action" })
            Assert.Contains($"{directive} 'none'", policy);
        Assert.DoesNotContain("unsafe-", policy);
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
        Assert.Equal("max-age=86400", response.Headers.GetValues("Strict-Transport-Security").Single());
        if (response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            Assert.True(response.Headers.CacheControl?.NoCache);
            Assert.True(response.Headers.CacheControl?.NoTransform);
        }
    }

    [Theory]
    [InlineData("Production", "http://demo.example")]
    [InlineData("Development", "https://demo.example")]
    [InlineData("Production", "https://localhost")]
    [InlineData("Production", "https://127.0.0.1")]
    public async Task Hsts_does_not_pin_http_development_or_loopback(string environment, string address)
    {
        await using var factory = Factory(environment: environment);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri(address), AllowAutoRedirect = false });
        var response = await client.GetAsync("/favicon.svg");
        var redirects = environment == "Production" && new Uri(address).Scheme == "http";
        Assert.Equal(redirects ? HttpStatusCode.PermanentRedirect : HttpStatusCode.OK, response.StatusCode);
        if (redirects) Assert.Equal("https://demo.example/favicon.svg", response.Headers.Location?.ToString());
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
