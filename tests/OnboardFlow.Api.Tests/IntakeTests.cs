using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

[Trait("Category", "Integration")]
public sealed class IntakeTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    internal sealed class FakeModel : IIntakeModelClient
    {
        public string Model => "deterministic-test-double";
        public int Calls { get; private set; }
        public string? Failure { get; init; }
        private static readonly string[][] Values =
        [
            ["legal","Juniper Studio LLC","profile","company"], ["dba","Juniper Studio","profile","company"],
            ["address","100 Example Avenue, Portland, OR 97205","profile","contact"], ["phone","+1 202-555-0147","profile","contact"],
            ["email","maya@juniper.example","profile","contact"], ["entity","LLC","profile","company"],
            ["website","https://juniper.example","profile","contact"], ["started","2021-04-12","profile","dates"],
            ["yearend","12/31","profile","dates"], ["activity","Branding and website design for small businesses","profile","activity"],
            ["bank-one","Demo Bank","setup","banking"], ["bank-two","No second bank account","setup","banking"],
            ["credit-card","Example Card","setup","banking"], ["access-alternative","Upload statements","setup","banking"],
            ["payroll","Payroll software","setup","systems"], ["crm","Example CRM","setup","systems"],
            ["loans","No","setup","loans"], ["call","An onboarding call would help","setup","call"]
        ];
        public Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation)
        {
            Calls++;
            if (Failure == "quota" && Calls == 3) throw new IntakeException(429, "Today's free AI allowance has been used.");
            if (Failure == "timeout") throw new TaskCanceledException();
            if (Failure == "unknown") return Task.FromResult(Tool("delete_everything"));
            if (Failure == "malformed") return Task.FromResult(new IntakeModelTurn(
                new JsonObject { ["role"] = "assistant", ["tool_calls"] = new JsonArray(new JsonObject { ["function"] = new JsonObject() }) }, 1, 1));
            if (access.FieldsAvailable)
                return Task.FromResult(Tools(("read_required_fields", null), ("read_client_document", "profile"), ("read_client_document", "setup")));
            if (Failure == "budget") return Task.FromResult(Output([], null, false));
            if (access.Documents.Contains("reply")) return Task.FromResult(Tools(("read_client_document", "reply")));
            if (messages.Any(x => x["tool_name"]?.GetValue<string>() == "read_client_document" && x["content"]!.GetValue<string>().Contains("\"id\":\"reply\"")))
                return Task.FromResult(Output([["tax", "DEMO-EIN-1042", "reply", "tax"]], null, true));
            var offset = (Calls - 2) * 8;
            var next = Values.Skip(offset).Take(8).ToArray();
            return Task.FromResult(Output(next, offset + 8 >= Values.Length ? new("tax", "What is the fictional demo tax reference?") : null,
                offset + 8 >= Values.Length && Failure != "question-not-done"));
        }
        private static IntakeModelTurn Tool(string name) => Tools((name, null));
        private static IntakeModelTurn Tools(params (string Name, string? Document)[] calls) => new(
            new JsonObject
            {
                ["role"] = "assistant", ["content"] = "",
                ["tool_calls"] = new JsonArray(calls.Select(call => (JsonNode)new JsonObject
                {
                    ["function"] = new JsonObject { ["name"] = call.Name,
                        ["arguments"] = call.Document is null ? new JsonObject() : new JsonObject { ["document"] = call.Document } }
                }).ToArray())
            }, 10, 1);
        private static IntakeModelTurn Output(string[][] values, IntakeQuestion? question, bool done) => new(
            new JsonObject { ["role"] = "assistant", ["content"] = System.Text.Json.JsonSerializer.Serialize(new { answers = values, question, done }, IntakeSpecification.Json) }, 100, 1);
    }
    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client, string Csrf)> Visitor(IIntakeModelClient model)
    {
        fixture.Clock.Now = DateTimeOffset.UtcNow;
        var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIntakeModelClient>();
            services.AddSingleton<IIntakeModelClient>(model);
        }));
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        await Post(client, csrf, "/api/session", new { });
        return (factory, client, csrf);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string csrf, string path, object data)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(data) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }
    private static async Task<IntakeView> Read(HttpClient client) => (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
    private static async Task<IntakeView> Command(HttpClient client, string csrf, string operation, string? field = null, string? value = null, string? page = null)
    {
        var current = await Read(client);
        var result = await Post(client, csrf, "/api/intake/command", new IntakeCommand(Guid.NewGuid(), current.Version, operation, field, value, page));
        result.EnsureSuccessStatusCode();
        return (await result.Content.ReadFromJsonAsync<IntakeView>())!;
    }
    [Fact]
    public async Task New_session_atomically_returns_current_request_without_legacy_project_rows()
    {
        using var client = fixture.Client();
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        var missing = await client.GetAsync("/api/intake/");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("sandbox_missing", (await missing.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        var created = await Post(client, csrf, "/api/session", new { });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = (await created.Content.ReadFromJsonAsync<JsonObject>())!;
        var session = body.Deserialize<SessionView>(IntakeSpecification.Json)!;
        var initial = body["intake"]!.Deserialize<IntakeView>(IntakeSpecification.Json)!;
        Assert.Equal(1, initial.Version);
        Assert.Equal("idle", initial.Ai);
        Assert.Empty(initial.Answers);
        Assert.Equal(TimeSpan.FromHours(24), session.ExpiresAt - session.CreatedAt);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        Assert.False(await db.Projects.AnyAsync(x => x.SandboxSessionId == session.Id));
        Assert.Equal(initial.Version, (await db.ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id)).Version);
        var loaded = await Read(client);
        Assert.Equal(initial.Version, loaded.Version);
        Assert.Empty(loaded.Answers);
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Count);
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Owned_read_uses_one_query_and_preserves_missing_request_and_expired_owner_handling()
    {
        var counter = new ReadCounter();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FoundationDb>(options => options.AddInterceptors(counter))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        var created = await Post(client, csrf, "/api/session", new { });
        created.EnsureSuccessStatusCode();
        var session = (await created.Content.ReadFromJsonAsync<SessionView>())!;
        counter.Count = 0;
        var snapshot = await Read(client);
        Assert.Equal(1, counter.Count);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<FoundationDb>().ClientRequests
                .Where(x => x.SandboxSessionId == session.Id).ExecuteDeleteAsync();
        var initialized = await Read(client);
        Assert.Equal(snapshot.Version, initialized.Version);
        Assert.Empty(initialized.Answers);
        fixture.Clock.Now = session.ExpiresAt;
        try
        {
            var expired = await client.GetAsync("/api/intake/");
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
            Assert.Equal("sandbox_unavailable", (await expired.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        }
        finally { fixture.Clock.Now = DateTimeOffset.UtcNow; }
    }
    [Fact]
    public async Task Real_pipeline_saves_drafts_requires_reply_never_completes_manual_items_and_replays_without_inference()
    {
        var model = new FakeModel(); var v = await Visitor(model); await using var factory = v.Factory;
        var initial = await Read(v.Client);
        Assert.Equal(initial.Version, (await Read(v.Client)).Version);
        var command = new AgentCommand(Guid.NewGuid(), initial.Version);
        var first = await Post(v.Client, v.Csrf, "/api/intake/agent", command);
        first.EnsureSuccessStatusCode();
        var state = (await first.Content.ReadFromJsonAsync<IntakeView>())!;
        Assert.Equal("waiting", state.Ai);
        Assert.Equal(18, state.Answers.Count);
        Assert.All(state.Answers.Values, x => { Assert.Equal("draft", x.Status); Assert.NotNull(x.Evidence); });
        foreach (var id in new[] { "tax", "tax-return", "folder", "payment", "ach" }) Assert.False(state.Answers.ContainsKey(id));
        await Post(v.Client, v.Csrf, "/api/intake/agent", command);
        Assert.Equal(4, model.Calls);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version, true))).StatusCode);
        state = await Command(v.Client, v.Csrf, "reply");
        var resumed = await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version, true));
        resumed.EnsureSuccessStatusCode();
        state = (await resumed.Content.ReadFromJsonAsync<IntakeView>())!;
        Assert.Equal("complete", state.Ai);
        Assert.Equal(6, model.Calls);
        Assert.Equal("reply", state.Answers["tax"].Evidence!.Document);
        state = await Command(v.Client, v.Csrf, "submitPage", page: "general");
        Assert.Equal(11, state.Answers.Values.Count(x => x.Status == "submitted"));
        state = await Command(v.Client, v.Csrf, "edit", "legal", "Changed LLC");
        Assert.Null(state.Answers["legal"].Evidence);
        Assert.Equal("draft", state.Answers["legal"].Status);
        Assert.Equal("you", state.Answers["legal"].Author);
        state = await Command(v.Client, v.Csrf, "reset");
        await Post(v.Client, v.Csrf, "/api/intake/agent", command);
        Assert.Equal(6, model.Calls);
        Assert.Empty((await Read(v.Client)).Answers);
    }
    [Theory]
    [InlineData("unknown", "failed")]
    [InlineData("malformed", "failed")]
    [InlineData("timeout", "interrupted")]
    [InlineData("budget", "failed")]
    public async Task Model_failures_remain_bounded_and_preserve_existing_human_work(string failure, string status)
    {
        var model = new FakeModel { Failure = failure }; var v = await Visitor(model); await using var factory = v.Factory;
        var state = await Command(v.Client, v.Csrf, "edit", "dba", "Saved human answer");
        await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version));
        state = await Read(v.Client);
        Assert.Equal(status, state.Ai);
        Assert.Equal("Saved human answer", state.Answers["dba"].Value);
        Assert.InRange(model.Calls, 1, 6);
        Assert.Equal(failure == "budget" ? 6 : 1, model.Calls);
        var calls = model.Calls; await Read(v.Client); Assert.Equal(calls, model.Calls);
    }
    [Fact]
    public async Task Explicit_production_rollout_preserves_ownership_and_csrf()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Intake:Enabled"] = "true", ["Intake:Provider"] = "OpenAI",
                    ["Intake:OpenAI:ProjectId"] = "proj_ducK0I8812r3DwpD89brnzC2",
                    ["Intake:OpenAI:ApiKey"] = "unit-test-placeholder", ["Intake:OpenAI:BudgetVerified"] = "true"
                }));
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/intake/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/intake/agent", new AgentCommand(Guid.NewGuid(), 0))).StatusCode);
    }
    [Theory]
    [InlineData("Ollama", "proj_ducK0I8812r3DwpD89brnzC2", true)]
    [InlineData("OpenAI", "proj_another", true)]
    [InlineData("OpenAI", "proj_ducK0I8812r3DwpD89brnzC2", false)]
    public void Production_refuses_an_unverified_provider_or_a_different_project(string provider, string project, bool verified)
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Intake:Enabled"] = "true", ["Intake:Provider"] = provider,
                    ["Intake:OpenAI:ProjectId"] = project,
                    ["Intake:OpenAI:ApiKey"] = "unit-test-placeholder", ["Intake:OpenAI:BudgetVerified"] = verified.ToString()
                }));
        });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
    [Fact]
    public async Task A_valid_clarification_pauses_without_wasting_calls_on_the_providers_done_flag()
    {
        var model = new FakeModel { Failure = "question-not-done" }; var v = await Visitor(model); await using var factory = v.Factory;
        var state = await Read(v.Client);
        await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version));
        state = await Read(v.Client);
        Assert.Equal("waiting", state.Ai);
        Assert.Equal(18, state.Answers.Count);
        Assert.Equal(4, model.Calls);
        Assert.False(state.Answers.ContainsKey("tax"));
    }
    [Fact]
    public async Task Exhausted_hosted_allowance_keeps_accepted_drafts_and_consumes_the_uncertain_call()
    {
        var model = new FakeModel { Failure = "quota" }; var v = await Visitor(model); await using var factory = v.Factory;
        var state = await Read(v.Client);
        var command = new AgentCommand(Guid.NewGuid(), state.Version);
        var response = await Post(v.Client, v.Csrf, "/api/intake/agent", command);
        response.EnsureSuccessStatusCode();
        state = await Read(v.Client);
        Assert.Equal("failed", state.Ai);
        Assert.Equal(8, state.Answers.Count);
        Assert.All(state.Answers.Values, answer => Assert.Equal("draft", answer.Status));
        Assert.Equal(3, model.Calls);
        var run = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(state.Run, IntakeSpecification.Json))!;
        Assert.Equal(3, run["calls"]!.GetValue<int>());
        Assert.Contains("free AI allowance", run["error"]!.GetValue<string>());
        await Post(v.Client, v.Csrf, "/api/intake/agent", command);
        await Read(v.Client);
        Assert.Equal(3, model.Calls);
        Assert.Equal(8, (await Read(v.Client)).Answers.Count);
        state = await Command(v.Client, v.Csrf, "edit", "tax", "DEMO-MANUAL-1042");
        Assert.Equal("you", state.Answers["tax"].Author);
    }
    [Fact]
    public async Task Commands_require_csrf_current_version_and_known_fields_and_validate_before_submission()
    {
        var v = await Visitor(new FakeModel()); await using var factory = v.Factory;
        var state = await Read(v.Client);
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.PostAsJsonAsync("/api/intake/command", new IntakeCommand(Guid.NewGuid(), state.Version, "reset"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(v.Client, v.Csrf, "/api/intake/command", new IntakeCommand(Guid.NewGuid(), state.Version, "edit", "unknown", "x"))).StatusCode);
        var id = Guid.NewGuid();
        var edit = new IntakeCommand(id, state.Version, "edit", "email", "invalid");
        var response = await Post(v.Client, v.Csrf, "/api/intake/command", edit); response.EnsureSuccessStatusCode();
        var after = await Read(v.Client);
        await Post(v.Client, v.Csrf, "/api/intake/command", edit);
        Assert.Equal(after.Version, (await Read(v.Client)).Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(v.Client, v.Csrf, "/api/intake/command", edit with { Value = "changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(v.Client, v.Csrf, "/api/intake/command", edit with { CommandId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(v.Client, v.Csrf, "/api/intake/command", new IntakeCommand(Guid.NewGuid(), after.Version, "submit", "email"))).StatusCode);
    }
    [Fact]
    public async Task Visitors_and_expired_sessions_cannot_share_request_state()
    {
        var first = await Visitor(new FakeModel()); await using var one = first.Factory;
        await Command(first.Client, first.Csrf, "edit", "legal", "Private fictional draft");
        var second = await Visitor(new FakeModel()); await using var two = second.Factory;
        Assert.Empty((await Read(second.Client)).Answers);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(25);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.Client.GetAsync("/api/intake/")).StatusCode);
        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }
    [Fact]
    public async Task Changed_source_version_invalidates_ai_drafts_but_keeps_human_work()
    {
        var v = await Visitor(new FakeModel()); await using var factory = v.Factory;
        var state = await Read(v.Client);
        await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version));
        await Command(v.Client, v.Csrf, "edit", "dba", "Human-entered name");
        var session = (await v.Client.GetFromJsonAsync<SessionView>("/api/session"))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
            var row = await db.ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id);
            var json = JsonNode.Parse(row.StateJson)!;
            json["specificationVersion"] = "old-source-version";
            row.StateJson = json.ToJsonString(); row.Version++;
            await db.SaveChangesAsync();
        }
        state = await Read(v.Client);
        Assert.Equal("failed", state.Ai);
        Assert.Single(state.Answers);
        Assert.Equal("Human-entered name", state.Answers["dba"].Value);
    }
    private sealed class HeldModel : IIntakeModelClient
    {
        public string Model => "held-test-double";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public async Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation)
        {
            if (Interlocked.Increment(ref Calls) > 1) throw new TaskCanceledException();
            Entered.SetResult(); await Release.Task.WaitAsync(cancellation);
            return new(new JsonObject { ["role"] = "assistant", ["content"] = "{\"answers\":[],\"question\":null,\"done\":false}" }, 1, 1);
        }
    }
    [Fact]
    public async Task An_unchanged_read_does_not_wait_for_the_sessions_write_lock()
    {
        var v = await Visitor(new FakeModel()); await using var factory = v.Factory;
        var before = await Read(v.Client);
        var session = (await v.Client.GetFromJsonAsync<SessionView>("/api/session"))!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT * FROM \"SandboxSessions\" WHERE \"Id\" = {session.Id} FOR UPDATE");
        var after = await Read(v.Client).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(before.Version, after.Version);
        Assert.Empty(after.Answers);
        await transaction.RollbackAsync();
    }
    [Fact]
    public async Task Reset_during_inference_accepts_a_stale_view_and_late_output_cannot_restore_work()
    {
        var model = new HeldModel(); var v = await Visitor(model); await using var factory = v.Factory;
        var session = (await v.Client.GetFromJsonAsync<SessionView>("/api/session"))!;
        var before = await Read(v.Client);
        var pending = Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), before.Version));
        await model.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var command = new IntakeCommand(Guid.NewGuid(), before.Version, "reset");
            var response = await Post(v.Client, v.Csrf, "/api/intake/command", command);
            response.EnsureSuccessStatusCode();
            var reset = await Read(v.Client);
            Assert.Equal("idle", reset.Ai);
            Assert.Null(reset.Run);
            Assert.Empty(reset.Answers);
            Assert.Empty(reset.Questions);
            Assert.False(reset.ReplyReady);
            await Post(v.Client, v.Csrf, "/api/intake/command", command);
            Assert.Equal(reset.Version, (await Read(v.Client)).Version);
            Assert.Equal(session, await v.Client.GetFromJsonAsync<SessionView>("/api/session"));
            Assert.Equal(HttpStatusCode.Conflict, (await Post(v.Client, v.Csrf, "/api/intake/command",
                new IntakeCommand(Guid.NewGuid(), reset.Version + 1, "reset"))).StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var row = await scope.ServiceProvider.GetRequiredService<FoundationDb>().ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id);
            Assert.Equal(1, JsonNode.Parse(row.StateJson)!["runsStarted"]!.GetValue<int>());
        }
        finally { model.Release.TrySetResult(); }
        await pending;
        var after = await Read(v.Client);
        Assert.Null(after.Run);
        Assert.Empty(after.Answers);
        Assert.Equal(1, model.Calls);
    }
    [Fact]
    public async Task Reset_clears_submissions_questions_and_reply_without_resetting_run_allowance()
    {
        var v = await Visitor(new FakeModel()); await using var factory = v.Factory;
        var before = await Read(v.Client);
        await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), before.Version));
        await Command(v.Client, v.Csrf, "reply");
        await Command(v.Client, v.Csrf, "question", "legal", "A fictional question");
        await Command(v.Client, v.Csrf, "submit", "legal");
        var reset = await Command(v.Client, v.Csrf, "reset");
        Assert.Empty(reset.Answers);
        Assert.Empty(reset.Questions);
        Assert.False(reset.ReplyReady);
        Assert.Null(reset.Run);
        var session = (await v.Client.GetFromJsonAsync<SessionView>("/api/session"))!;
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<FoundationDb>().ClientRequests.SingleAsync(x => x.SandboxSessionId == session.Id);
        Assert.Equal(1, JsonNode.Parse(row.StateJson)!["runsStarted"]!.GetValue<int>());
    }
    [Fact]
    public async Task Concurrent_start_is_rejected_and_an_expired_claim_cannot_overwrite_a_new_run()
    {
        var model = new HeldModel(); var v = await Visitor(model); await using var factory = v.Factory;
        var before = await Read(v.Client);
        var pending = Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), before.Version));
        await model.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var running = await Read(v.Client);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), running.Version))).StatusCode);
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(121);
        var interrupted = await Read(v.Client);
        Assert.Equal("interrupted", interrupted.Ai);
        await Post(v.Client, v.Csrf, "/api/intake/agent", new AgentCommand(Guid.NewGuid(), interrupted.Version));
        var newer = await Read(v.Client);
        model.Release.SetResult(); await pending;
        var final = await Read(v.Client);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(newer.Run), System.Text.Json.JsonSerializer.Serialize(final.Run));
        Assert.Equal(2, model.Calls);
        Assert.Empty(final.Answers);
        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }
    [Fact]
    public async Task Production_does_not_expose_the_local_agent_endpoint()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("environment", "Production"));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/intake/")).StatusCode);
    }
}

[Trait("Category", "Unit")]
public sealed class IntakeRulesTests
{
    [Theory]
    [InlineData("payment", "done", "setup", "actions")]
    [InlineData("unknown", "x", "profile", "company")]
    [InlineData("legal", "LLC", "invented", "company")]
    [InlineData("legal", "LLC", "setup", "banking")]
    [InlineData("entity", "Invented entity", "profile", "company")]
    [InlineData("email", "not-an-email", "profile", "contact")]
    [InlineData("yearend", "2021-12-31", "profile", "dates")]
    [InlineData("tax", "not included", "profile", "activity")]
    [InlineData("tax", "DEMO-INVENTED", "profile", "activity")]
    [InlineData("tax", "123-45-6789", "profile", "activity")]
    public void Invalid_fields_manual_completion_and_invalid_sources_are_rejected(string id, string value, string document, string section)
    {
        var proposals = new JsonArray(new JsonArray(id, value, document, section));
        Assert.Throws<IntakeException>(() => IntakeRules.ValidateDrafts(proposals, new(), ["profile", "setup"], new Dictionary<string, IntakeAnswer>()));
    }
    [Fact]
    public void Fictional_identifiers_must_match_the_whole_source_identifier()
    {
        var spec = new IntakeSpecification();
        var existing = new Dictionary<string, IntakeAnswer>();
        Assert.Throws<IntakeException>(() => IntakeRules.ValidateDrafts(
            new JsonArray(new JsonArray("tax", "DEMO-EIN", "reply", "tax")), spec, ["reply"], existing));
        var valid = IntakeRules.ValidateDrafts(
            new JsonArray(new JsonArray("tax", "DEMO-EIN-1042", "reply", "tax")), spec, ["reply"], existing);
        Assert.Equal("DEMO-EIN-1042", valid["tax"].Value);
    }
    [Fact]
    public void Quotes_are_taken_from_the_retrieved_document_not_model_text()
    {
        var spec = new IntakeSpecification();
        var answer = IntakeRules.ValidateDrafts(new JsonArray(new JsonArray("legal", "Juniper Studio LLC", "profile", "company")), spec, ["profile"], new Dictionary<string, IntakeAnswer>())["legal"];
        Assert.Equal(spec.Documents["profile"].Sections[0].Text, answer.Evidence!.Quote);
        Assert.Equal("draft", answer.Status);
    }
}
