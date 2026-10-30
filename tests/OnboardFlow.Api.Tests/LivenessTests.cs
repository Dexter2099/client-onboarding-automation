using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OnboardFlow.Api.Tests;

public sealed class LivenessTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.9.83")]
    [InlineData("onboardflow.hostless.app")]
    public async Task Get_health_accepts_internal_probe_hosts_without_database_or_cookie(string host)
    {
        await using var factory = Factory();
        var response = await Send(factory, host, "GET", "/health");
        Assert.Equal(200, response.Response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await new StreamReader(response.Response.Body).ReadToEndAsync());
        Assert.Equal("no-store", response.Response.Headers.CacheControl.ToString());
        Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(response.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("GET", "/api/csrf")]
    [InlineData("GET", "/health/ready")]
    [InlineData("GET", "/health/other")]
    [InlineData("POST", "/health")]
    public async Task Other_requests_keep_the_production_host_filter(string method, string path)
    {
        await using var factory = Factory();
        var response = await Send(factory, "10.0.9.83", method, path);
        Assert.Equal(400, response.Response.StatusCode);
        Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
    }

    private static WebApplicationFactory<Program> Factory()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AllowedHosts"] = "onboardflow.hostless.app",
                    ["ConnectionStrings:OnboardFlow"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                    ["VerifiedEdge:Enabled"] = "false"
                }));
        });

    private static Task<HttpContext> Send(WebApplicationFactory<Program> factory, string host, string method, string path)
        => factory.Server.SendAsync(context =>
        {
            context.Request.Scheme = "http";
            context.Request.Host = new HostString(host);
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.9.1");
            context.Request.Method = method;
            context.Request.Path = path;
        });
}
