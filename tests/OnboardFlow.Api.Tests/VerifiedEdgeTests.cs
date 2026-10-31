using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OnboardFlow.Api.Tests;

public sealed class VerifiedEdgeTests
{
    private const string Origin = "https://dexter-onboardflow.pages.dev";

    [Theory]
    [InlineData("10.0.7.54")]
    [InlineData("10.0.99.201")]
    [InlineData("203.0.113.7")]
    public async Task Signed_https_works_after_proxy_rotation_and_keeps_secure_cookies(string peer)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var response = await Send(factory, key, peer: peer);
        Assert.Equal(200, response.Response.StatusCode);
        Assert.Contains("secure", response.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", response.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", response.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("max-age=86400", response.Response.Headers.StrictTransportSecurity.ToString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("overflow")]
    [InlineData("other-key")]
    [InlineData("path")]
    [InlineData("body")]
    [InlineData("method")]
    [InlineData("duplicate")]
    public async Task Forged_or_replayed_evidence_cannot_grant_https_or_a_cookie(string fault)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var response = await Send(factory, key, fault: fault);
        Assert.Equal(fault == "method" ? 403 : 308, response.Response.StatusCode);
        Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.False(response.Response.Headers.ContainsKey("Strict-Transport-Security"));
        if (fault != "method") Assert.Equal(Origin + "/api/csrf", response.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Valid_edge_signature_does_not_bypass_csrf()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var response = await Send(factory, key, method: "POST", path: "/api/session");
        Assert.Equal(403, response.Response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Response.ContentType);
    }

    [Fact]
    public async Task Signed_oversized_body_is_rejected_before_dispatch()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var response = await Send(factory, key, method: "POST", body: new string('x', 16385));
        Assert.Equal(413, response.Response.StatusCode);
        Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Encoded_query_is_bound_to_the_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var response = await Send(factory, key, query: "?probe=a%2Fb&x=%25");
        Assert.Equal(200, response.Response.StatusCode);
        Assert.NotNull(response.Features.Get<VerifiedCaller>());
    }

    [Theory]
    [InlineData("", "/api/csrf")]
    [InlineData("forged", "/api/csrf")]
    [InlineData("duplicate", "/api/csrf")]
    [InlineData("", "/health/ready/")]
    [InlineData("", "/HeAlTh/ReAdY/")]
    public async Task A_valid_signature_without_one_well_formed_network_bucket_cannot_dispatch(string network, string path)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var factory = Factory(key);
        var query = network == "" ? "?without=network" : network == "duplicate"
            ? "?__of_network=" + new string('a', 64) + "&__of_network=" + new string('b', 64)
            : "?__of_network=forged";
        var response = await Send(factory, key, query: query, addNetwork: false, path: path);
        Assert.Equal(403, response.Response.StatusCode);
        Assert.Null(response.Features.Get<VerifiedCaller>());
        Assert.False(response.Response.Headers.ContainsKey("Set-Cookie"));
    }

    private static WebApplicationFactory<Program> Factory(ECDsa key)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OnboardFlow"] = "Host=127.0.0.1;Database=unused;Username=unused",
                    ["VerifiedEdge:Enabled"] = "true",
                    ["VerifiedEdge:PublicOrigin"] = Origin,
                    ["VerifiedEdge:PublicKey"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
                }));
        });

    private static Task<HttpContext> Send(WebApplicationFactory<Program> factory, ECDsa key,
        string peer = "203.0.113.7", string fault = "", string method = "GET", string path = "/api/csrf",
        string query = "", string body = "", bool addNetwork = true)
    {
        if (addNetwork && fault == "") query += (query.Length == 0 ? "?" : "&") + "__of_network=" + new string('a', 64);
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (fault == "expired" ? -120 : fault == "future" ? 120 : 0);
        var timestamp = fault == "overflow" ? long.MaxValue.ToString(CultureInfo.InvariantCulture) : seconds.ToString(CultureInfo.InvariantCulture);
        var signedPath = fault == "path" ? "/other" : path;
        var canonical = string.Join('\n', "v1", timestamp, Origin, method, signedPath + query,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = Convert.ToBase64String((fault == "other-key" ? otherKey : key).SignData(
            Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        return factory.Server.SendAsync(context =>
        {
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("onboardflow.hostless.app");
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Method = fault == "method" ? "POST" : method;
            context.Request.Path = path;
            context.Request.QueryString = new QueryString(query);
            var bytes = Encoding.UTF8.GetBytes(fault == "body" ? "changed" : body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["CF-Visitor"] = "{\"scheme\":\"https\"}";
            if (fault != "missing")
            {
                context.Request.Headers["X-OnboardFlow-Edge-Time"] = fault == "duplicate" ? new[] { timestamp, timestamp } : new[] { timestamp };
                context.Request.Headers["X-OnboardFlow-Edge-Signature"] = fault == "malformed" ? new string('!', 88) : signature;
            }
        });
    }
}
