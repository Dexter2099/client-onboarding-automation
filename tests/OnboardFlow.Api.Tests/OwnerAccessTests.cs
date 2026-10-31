using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class OwnerAccessTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>, IDisposable
{
    private const string Origin = "https://localhost";
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public void Dispose() => key.Dispose();
    private Dictionary<string, string?> Settings() => new()
    {
        ["VerifiedEdge:PublicOrigin"] = Origin,
        ["OwnerAccess:PublicKey"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())
    };
    private string Pass(DateTimeOffset? expiration = null, string origin = Origin, string purpose = OwnerAccess.Purpose, ECDsa? signer = null)
    {
        var expires = (expiration ?? fixture.Clock.GetUtcNow().AddDays(90)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var signature = (signer ?? key).SignData(Encoding.UTF8.GetBytes(string.Join('\n', purpose, origin, expires, nonce)),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"v1.{expires}.{nonce}.{WebEncoders.Base64UrlEncode(signature)}";
    }
    [Theory]
    [InlineData("valid")]
    [InlineData("expired")]
    [InlineData("too-far")]
    [InlineData("wrong-origin")]
    [InlineData("gateway-signature")]
    [InlineData("other-key")]
    [InlineData("tampered")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    public void Only_a_current_owner_scoped_signature_is_accepted(string scenario)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build();
        var owner = new OwnerAccess(config, fixture.Clock);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pass = scenario switch
        {
            "expired" => Pass(fixture.Clock.GetUtcNow().AddSeconds(-1)),
            "too-far" => Pass(fixture.Clock.GetUtcNow().AddDays(91)),
            "wrong-origin" => Pass(origin: "https://somebody.example"),
            "gateway-signature" => Pass(purpose: "v1"),
            "other-key" => Pass(signer: other),
            "malformed" => "v1.bad.invalid.signature",
            "oversized" => new string('x', 257),
            _ => Pass()
        };
        if (scenario == "tampered") pass = pass.Replace("v1.", "v1.1");
        Assert.Equal(scenario == "valid", owner.Expires(pass) is not null);
        Assert.Null(owner.Expires(null));
        Assert.Null(new OwnerAccess(new ConfigurationBuilder().Build(), fixture.Clock).Expires(pass));
    }

    [Fact]
    public async Task Private_activation_bypasses_only_owner_run_count_and_preserves_public_limits_reset_and_expiry()
    {
        var model = new IntakeTests.FakeModel { Failure = "timeout" }; // No inference.
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIntakeModelClient>();
                services.AddSingleton<IIntakeModelClient>(model);
            });
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri(Origin), AllowAutoRedirect = false });
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        async Task<HttpResponseMessage> Post(HttpClient target, string path, object body, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-CSRF-TOKEN", token);
            return await target.SendAsync(request);
        }
        (await Post(client, "/api/session", new { }, csrf)).EnsureSuccessStatusCode();
        var session = (await client.GetFromJsonAsync<SessionView>("/api/session"))!;
        async Task Exhaust(Guid owner)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
            var row = await db.ClientRequests.SingleAsync(x => x.SandboxSessionId == owner);
            var state = JsonSerializer.Deserialize<IntakeState>(row.StateJson, IntakeSpecification.Json)!;
            state.RunsStarted = 3;
            row.StateJson = JsonSerializer.Serialize(state, IntakeSpecification.Json); row.Version++;
            await db.SaveChangesAsync();
        }
        async Task<HttpResponseMessage> Run(HttpClient target, string token)
        {
            var snapshot = (await target.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
            // A client-supplied owner flag cannot grant the server-side capability.
            return await Post(target, "/api/intake/agent", new { commandId = Guid.NewGuid(), expectedVersion = snapshot.Version, ownerAccess = true }, token);
        }
        await Exhaust(session.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Run(client, csrf)).StatusCode);
        Assert.Equal(0, model.Calls);
        var page = await client.GetAsync("/api/owner-access");
        Assert.True(page.Headers.CacheControl!.NoStore);
        var html = await page.Content.ReadAsStringAsync();
        var formToken = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(formToken);
        var pass = Pass();
        FormUrlEncodedContent Form(string value, bool protection = true, string action = "enable") => new(
            new Dictionary<string, string> { ["pass"] = value, ["action"] = action, ["__RequestVerificationToken"] = protection ? formToken : "" });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/owner-access", Form(pass, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/owner-access", Form("not-an-owner"))).StatusCode);
        var activation = await client.PostAsync("/api/owner-access", Form(pass));
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        Assert.Null(activation.Headers.Location);
        Assert.True(activation.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain(pass, await activation.Content.ReadAsStringAsync());
        var cookie = activation.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(OwnerAccess.CookieName + "="));
        foreach (var flag in new[] { "secure", "httponly", "samesite=strict", "path=/", "max-age=" })
            Assert.Contains(flag, cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        var enabled = await client.GetAsync("/api/owner-access");
        Assert.True(enabled.Headers.CacheControl!.NoStore);
        Assert.Contains("Owner access enabled", await enabled.Content.ReadAsStringAsync());
        (await Run(client, csrf)).EnsureSuccessStatusCode();
        Assert.Equal(1, model.Calls);
        var current = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        (await Post(client, "/api/intake/command", new IntakeCommand(Guid.NewGuid(), current.Version, "reset"), csrf)).EnsureSuccessStatusCode();
        Assert.Equal(session, await client.GetFromJsonAsync<SessionView>("/api/session"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<FoundationDb>().ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id);
            Assert.Equal(4, JsonSerializer.Deserialize<IntakeState>(row.StateJson, IntakeSpecification.Json)!.RunsStarted);
        }
        fixture.Clock.Now = session.ExpiresAt;
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/intake/")).StatusCode);
            Assert.Contains("Owner access enabled", await client.GetStringAsync("/api/owner-access"));
        }
        finally { fixture.Clock.Now = DateTimeOffset.UtcNow; }
        using var visitor = factory.CreateClient(new() { BaseAddress = new Uri(Origin), AllowAutoRedirect = false });
        var visitorCsrf = (await visitor.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        (await Post(visitor, "/api/session", new { }, visitorCsrf)).EnsureSuccessStatusCode();
        await Exhaust((await visitor.GetFromJsonAsync<SessionView>("/api/session"))!.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Run(visitor, visitorCsrf)).StatusCode);
        Assert.Equal(1, model.Calls);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/owner-access", Form("", action: "disable"))).StatusCode);
        Assert.Contains("Enable owner access", await client.GetStringAsync("/api/owner-access"));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Run(client, csrf)).StatusCode);
        Assert.Equal(1, model.Calls);
    }
}
