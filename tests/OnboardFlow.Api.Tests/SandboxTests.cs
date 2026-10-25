using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

[Trait("Acceptance", "AC-07, AC-11 foundation")]
[Trait("Category", "Integration")]
public sealed class SandboxTests : IClassFixture<FoundationFixture>
{
    private readonly FoundationFixture fixture;
    public SandboxTests(FoundationFixture fixture)
    {
        this.fixture = fixture;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }

    private sealed record Visitor(HttpClient Client, SessionView Session, string Token,
        string CsrfCookie, string AuthCookie, ProjectView[] Projects);

    private async Task<Visitor> Start()
    {
        var client = fixture.Client();
        var protection = await client.GetAsync("/api/csrf");
        var token = (await protection.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["token"];
        var csrfCookie = protection.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var response = await Post(client, "/api/session", token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authCookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var session = (await response.Content.ReadFromJsonAsync<SessionView>())!;
        var projects = (await client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
        return new Visitor(client, session, token, csrfCookie, authCookie, projects);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Creates_two_independent_signed_projects_with_24_hour_lifetime()
    {
        var visitor = await Start();
        Assert.Equal(TimeSpan.FromHours(24), visitor.Session.ExpiresAt - visitor.Session.CreatedAt);
        Assert.Equal(["OF-1042", "OF-1043"], visitor.Projects.Select(x => x.Reference));
        Assert.Equal(2, visitor.Projects.Select(x => x.Id).Distinct().Count());
        Assert.All(visitor.Projects, project =>
        {
            Assert.Equal("Juniper Studio", project.ClientName);
            Assert.Equal("Maya Ellis", project.ClientContact);
            Assert.Equal("Alex Morgan", project.Owner);
            Assert.Equal(["Home", "About", "Services", "Projects", "Contact"], project.SignedPages);
        });
    }

    [Fact]
    public async Task Refresh_and_repeated_start_retain_identity_and_never_extend_expiry()
    {
        var visitor = await Start();
        fixture.Clock.Now = visitor.Session.CreatedAt.AddHours(12);
        var repeated = await Post(visitor.Client, "/api/session", visitor.Token);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.False(repeated.Headers.Contains("Set-Cookie"));
        Assert.Equal(visitor.Session, await repeated.Content.ReadFromJsonAsync<SessionView>());
        Assert.Equal(visitor.Session, await visitor.Client.GetFromJsonAsync<SessionView>("/api/session"));
        var projects = (await visitor.Client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
        Assert.Equal(visitor.Projects.Select(x => x.Id), projects.Select(x => x.Id));
    }

    [Fact]
    public async Task Other_visitor_cannot_read_project_even_with_its_ID_or_session_query_parameter()
    {
        var owner = await Start();
        var other = await Start();
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Client.GetAsync($"/api/projects/{owner.Projects[0].Id}")).StatusCode);
        var own = (await other.Client.GetFromJsonAsync<ProjectView[]>(
            $"/api/projects?sessionId={owner.Session.Id}"))!;
        Assert.Equal(other.Projects.Select(x => x.Id), own.Select(x => x.Id));
        Assert.Equal(HttpStatusCode.OK,
            (await owner.Client.GetAsync($"/api/projects/{owner.Projects[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task Reset_replaces_both_projects_and_revokes_old_cookie_without_affecting_other_visitor()
    {
        var owner = await Start();
        var other = await Start();
        var reset = await Post(owner.Client, "/api/session/reset", owner.Token);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var replacement = (await reset.Content.ReadFromJsonAsync<SessionView>())!;
        Assert.NotEqual(owner.Session.Id, replacement.Id);
        var projects = (await owner.Client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
        Assert.Equal(2, projects.Length);
        Assert.Empty(owner.Projects.Select(x => x.Id).Intersect(projects.Select(x => x.Id)));
        Assert.Equal(other.Projects.Select(x => x.Id),
            (await other.Client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!.Select(x => x.Id));

        using var old = fixture.Client(false);
        old.DefaultRequestHeaders.Add("Cookie", $"{owner.AuthCookie}; {owner.CsrfCookie}");
        foreach (var path in new[] { "/api/session", "/api/projects", $"/api/projects/{owner.Projects[0].Id}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(old, "/api/session/reset", owner.Token)).StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        Assert.False(await db.SandboxSessions.AnyAsync(x => x.Id == owner.Session.Id));
        Assert.False(await db.Projects.AnyAsync(x => x.SandboxSessionId == owner.Session.Id));
    }

    [Fact]
    public async Task Overlapping_resets_allow_only_one_replacement()
    {
        var visitor = await Start();
        using var first = fixture.Client(false);
        using var second = fixture.Client(false);
        foreach (var client in new[] { first, second })
            client.DefaultRequestHeaders.Add("Cookie", $"{visitor.AuthCookie}; {visitor.CsrfCookie}");
        var responses = await Task.WhenAll(Post(first, "/api/session/reset", visitor.Token),
            Post(second, "/api/session/reset", visitor.Token));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Access_stops_at_exact_deadline_before_physical_cleanup_and_requires_explicit_fresh_start()
    {
        var visitor = await Start();
        fixture.Clock.Now = visitor.Session.CreatedAt.AddHours(23);
        var laterVisitor = await Start();
        fixture.Clock.Now = visitor.Session.ExpiresAt.AddTicks(-10);
        Assert.Equal(HttpStatusCode.OK, (await visitor.Client.GetAsync("/api/projects")).StatusCode);
        fixture.Clock.Now = visitor.Session.ExpiresAt;
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.Client.GetAsync("/api/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.Client.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(visitor.Client, "/api/session/reset", visitor.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laterVisitor.Client.GetAsync("/api/projects")).StatusCode);

        await using (var before = fixture.Factory.Services.CreateAsyncScope())
            Assert.True(await before.ServiceProvider.GetRequiredService<FoundationDb>()
                .SandboxSessions.AnyAsync(x => x.Id == visitor.Session.Id));

        var fresh = await Post(visitor.Client, "/api/session", visitor.Token);
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
        Assert.NotEqual(visitor.Session.Id, (await fresh.Content.ReadFromJsonAsync<SessionView>())!.Id);
        var projects = (await visitor.Client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
        Assert.Empty(visitor.Projects.Select(x => x.Id).Intersect(projects.Select(x => x.Id)));
        await using var after = fixture.Factory.Services.CreateAsyncScope();
        Assert.False(await after.ServiceProvider.GetRequiredService<FoundationDb>()
            .SandboxSessions.AnyAsync(x => x.Id == visitor.Session.Id));
        Assert.Equal(HttpStatusCode.OK, (await laterVisitor.Client.GetAsync("/api/projects")).StatusCode);
    }

    [Fact]
    public async Task Missing_or_forged_cookie_cannot_read_or_reset_a_sandbox()
    {
        using var missing = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await missing.GetAsync("/api/projects")).StatusCode);
        var visitor = await Start();
        using var forged = fixture.Client(false);
        forged.DefaultRequestHeaders.Add("Cookie", $"{SandboxEndpoints.CookieName}={new string('A', 64)}; {visitor.CsrfCookie}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(forged, "/api/session/reset", visitor.Token)).StatusCode);
    }

    [Fact]
    public async Task Cookies_are_secure_and_only_the_token_hash_is_stored()
    {
        var visitor = await Start();
        var reset = await Post(visitor.Client, "/api/session/reset", visitor.Token);
        var cookie = reset.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var session = (await reset.Content.ReadFromJsonAsync<SessionView>())!;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<FoundationDb>()
            .SandboxSessions.SingleAsync(x => x.Id == session.Id);
        Assert.Equal(64, saved.TokenHash.Length);
        Assert.DoesNotContain(saved.TokenHash, cookie);
        Assert.Equal("no-store", reset.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Missing_or_invalid_csrf_protection_blocks_writes()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/session", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/session", "forged")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/session")).StatusCode);
    }

    [Fact]
    public async Task PostgreSQL_enforces_unique_reference_within_a_sandbox()
    {
        var visitor = await Start();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        var project = await db.Projects.AsNoTracking().SingleAsync(x => x.Id == visitor.Projects[0].Id);
        project.Id = Guid.NewGuid();
        db.Projects.Add(project);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)error.InnerException!).SqlState);
    }

    [Fact]
    public async Task PostgreSQL_rejects_a_sliding_or_invalid_lifetime()
    {
        var visitor = await Start();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        var session = await db.SandboxSessions.SingleAsync(x => x.Id == visitor.Session.Id);
        session.ExpiresAt = session.ExpiresAt.AddMinutes(1);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ((PostgresException)error.InnerException!).SqlState);
    }

    [Fact]
    public async Task Unknown_API_and_out_of_scope_reminder_route_do_not_return_the_app_shell()
    {
        var visitor = await Start();
        var unknown = await visitor.Client.GetAsync("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType!.MediaType);
        var workflow = await Post(visitor.Client, $"/api/projects/{visitor.Projects[0].Id}/remind", visitor.Token);
        Assert.Equal(HttpStatusCode.NotFound, workflow.StatusCode);
    }
}
