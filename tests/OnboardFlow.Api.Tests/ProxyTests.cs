using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OnboardFlow.Api.Tests;

[Trait("Category", "Integration")]
public sealed class ProxyTests
{
    [Theory]
    [InlineData("192.0.2.10", "https", 200)]
    [InlineData("192.0.2.11", "https", 308)]
    [InlineData("192.0.2.10", null, 308)]
    public async Task Production_csrf_requires_https_from_a_trusted_proxy(
        string remoteAddress, string? forwardedScheme, int expectedStatus)
        => await AssertCsrf(remoteAddress, forwardedScheme, expectedStatus);

    [Theory]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"https\"}", "http", 200)]
    [InlineData(false, "192.0.2.10", "{\"scheme\":\"https\"}", "http", 308)]
    [InlineData(true, "192.0.2.11", "{\"scheme\":\"https\"}", "https", 308)]
    [InlineData(true, "192.0.2.10", null, "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"http\"}", "https", 308)]
    [InlineData(true, "192.0.2.10", "not-json", "https", 308)]
    [InlineData(true, "192.0.2.10", "{}", "https", 308)]
    [InlineData(true, "192.0.2.10", "[]", "https", 308)]
    [InlineData(true, "192.0.2.10", "null", "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":true}", "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"ftp\"}", "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"http\",\"scheme\":\"https\"}", "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"https\",\"extra\":true}", "https", 308)]
    [InlineData(true, "192.0.2.10", "{\"scheme\":\"http\"},{\"scheme\":\"https\"}", "https", 308)]
    public async Task Cloudflare_scheme_requires_opt_in_exact_peer_and_valid_evidence(
        bool enabled, string remoteAddress, string? visitor, string forwardedScheme, int expectedStatus)
        => await AssertCsrf(remoteAddress, forwardedScheme, expectedStatus, enabled, visitor);

    private static async Task AssertCsrf(string remoteAddress, string? forwardedScheme, int expectedStatus,
        bool useCloudflareVisitor = false, string? visitor = null)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OnboardFlow"] = "Host=127.0.0.1;Database=unused;Username=unused",
                    ["ReverseProxy:KnownProxies:0"] = "192.0.2.10",
                    ["ReverseProxy:UseCloudflareVisitor"] = useCloudflareVisitor.ToString()
                }));
        });
        var response = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Scheme = "http";
            context.Request.Path = "/api/csrf";
            context.Request.Host = new HostString("demo.example");
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            if (forwardedScheme is not null)
                context.Request.Headers["X-Forwarded-Proto"] = forwardedScheme;
            if (visitor is not null) context.Request.Headers["CF-Visitor"] = visitor;
            context.Request.Headers["X-OnboardFlow-Proto"] = "https";
        });

        Assert.Equal(expectedStatus, response.Response.StatusCode);
        Assert.Equal(expectedStatus == 200,
            response.Response.Headers.ContainsKey("Strict-Transport-Security"));
        if (expectedStatus == 200)
        {
            var cookie = response.Response.Headers.SetCookie.ToString();
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        }
        else Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
    }
}
