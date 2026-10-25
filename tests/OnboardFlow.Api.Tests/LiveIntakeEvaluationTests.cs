using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class LocalAiTheoryAttribute : TheoryAttribute
{
    public LocalAiTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("ONBOARDFLOW_LIVE_AI") != "1")
            Skip = "Opt-in provider evaluation; never call a model in ordinary CI.";
    }
}

[Trait("Category", "LiveAI")]
public sealed class LiveIntakeEvaluationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private sealed class ResponseShapeHandler(List<object> shapes) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            var response = await base.SendAsync(request, cancellation);
            await response.Content.LoadIntoBufferAsync(65536, cancellation);
            var text = await response.Content.ReadAsStringAsync(cancellation);
            try
            {
                var json = System.Text.Json.Nodes.JsonNode.Parse(text);
                var usage = (json?["result"] ?? json)?["usage"];
                var errors = (json?["errors"] as System.Text.Json.Nodes.JsonArray)?.Take(3).Select(error => new
                {
                    code = Number(error?["code"]),
                    message = System.Text.RegularExpressions.Regex.Replace(
                        (error?["message"]?.GetValue<string>() ?? "")[..Math.Min(300, (error?["message"]?.GetValue<string>() ?? "").Length)],
                        "[A-Za-z0-9_-]{32,}", "[redacted]")
                }).ToArray();
                var providerError = json?["error"] is System.Text.Json.Nodes.JsonObject singleError ? new
                {
                    code = singleError["code"]?.ToString(),
                    message = System.Text.RegularExpressions.Regex.Replace(
                        (singleError["message"]?.GetValue<string>() ?? "")[..Math.Min(300, (singleError["message"]?.GetValue<string>() ?? "").Length)],
                        "[A-Za-z0-9_-]{32,}", "[redacted]")
                } : null;
                shapes.Add(new { status = (int)response.StatusCode, errors, providerError, shape = Shape(json, 5), usage = new
                {
                    inputTokens = Number(usage?["prompt_tokens"]), outputTokens = Number(usage?["completion_tokens"]),
                    reportedNeurons = Number(usage?["neurons"])
                } });
            }
            catch (JsonException) { shapes.Add(new { status = (int)response.StatusCode, shape = "unreadable" }); }
            return response;
        }
        private static decimal? Number(System.Text.Json.Nodes.JsonNode? value) =>
            value is System.Text.Json.Nodes.JsonValue number && number.TryGetValue<decimal>(out var result) ? result : null;
        private static object Shape(System.Text.Json.Nodes.JsonNode? node, int depth) => node switch
        {
            null => "null",
            System.Text.Json.Nodes.JsonObject obj when depth > 0 => obj.ToDictionary(x => x.Key, x => Shape(x.Value, depth - 1)),
            System.Text.Json.Nodes.JsonArray array when depth > 0 => new { count = array.Count, first = Shape(array.FirstOrDefault(), depth - 1) },
            _ => node.GetValueKind().ToString()
        };
    }
    private sealed class RecordingModel(IIntakeModelClient inner, List<IntakeModelTurn> turns) : IIntakeModelClient
    {
        public string Model => inner.Model;
        public async Task<IntakeModelTurn> Turn(IReadOnlyList<System.Text.Json.Nodes.JsonObject> messages,
            IntakeToolAccess access, CancellationToken cancellation)
        {
            var turn = await inner.Turn(messages, access, cancellation);
            // Adapter-normalized output only: no credential, request headers or hidden reasoning.
            turns.Add(turn);
            return turn;
        }
    }
    [LocalAiTheory]
    [InlineData("complete", 1)] [InlineData("complete", 2)] [InlineData("complete", 3)]
    [InlineData("missing", 1)] [InlineData("missing", 2)] [InlineData("missing", 3)]
    [InlineData("conflict", 1)] [InlineData("conflict", 2)] [InlineData("conflict", 3)]
    public async Task Source_quality_and_action_boundaries(string scenario, int repetition)
    {
        var specification = new IntakeSpecification();
        if (scenario is "complete" or "conflict")
        {
            var original = specification.Documents["profile"];
            var sections = original.Sections.ToArray();
            sections[3] = sections[3] with { Text = "We provide branding and website design for small businesses. The fictional demo tax reference is DEMO-EIN-COMPLETE." };
            if (scenario == "conflict")
                sections[0] = sections[0] with { Text = "Our trading name is Juniper Studio and our entity is LLC. Two documents conflict: one gives the legal company name as Juniper Studio LLC, another as Juniper Studio Inc. The correct legal name is unresolved; ask the client to clarify rather than choosing either." };
            specification.Documents["profile"] = original with { Sections = sections };
        }
        var turns = new List<IntakeModelTurn>();
        var responseShapes = new List<object>();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            if (Environment.GetEnvironmentVariable("Intake__Provider") == "OpenAI")
            {
                // Real costs must survive deletion of each disposable request-test database.
                var ledger = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__OnboardFlow"));
                if (ledger.Host != "127.0.0.1" || ledger.Port != 54329 || ledger.Database != "onboardflow_dev" || ledger.Username != "onboardflow")
                    throw new InvalidOperationException("Live evaluations require the persistent project-local spend ledger.");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Intake:SpendConnectionString"] = ledger.ConnectionString }));
            }
            builder.ConfigureServices(services =>
            {
            services.RemoveAll<IntakeSpecification>();
            services.AddSingleton(specification);
            services.AddHttpClient<CloudflareIntakeClient>().AddHttpMessageHandler(() => new ResponseShapeHandler(responseShapes));
            services.AddHttpClient<OpenAiIntakeClient>().AddHttpMessageHandler(() => new ResponseShapeHandler(responseShapes));
            var registration = services.Last(x => x.ServiceType == typeof(IIntakeModelClient));
            services.RemoveAll<IIntakeModelClient>();
            services.AddScoped<IIntakeModelClient>(provider => new RecordingModel(
                (IIntakeModelClient)registration.ImplementationFactory!(provider), turns));
            });
        });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var token = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        async Task<HttpResponseMessage> Post(string path, object data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(data) };
            request.Headers.Add("X-CSRF-TOKEN", token);
            return await client.SendAsync(request);
        }
        await Post("/api/session", new { });
        var state = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        var watch = Stopwatch.StartNew();
        var response = await Post("/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version));
        response.EnsureSuccessStatusCode();
        state = (await response.Content.ReadFromJsonAsync<IntakeView>())!;
        var initial = state;
        if (scenario == "missing" && state.Ai == "waiting")
        {
            response = await Post("/api/intake/command", new IntakeCommand(Guid.NewGuid(), state.Version, "reply"));
            response.EnsureSuccessStatusCode();
            state = (await response.Content.ReadFromJsonAsync<IntakeView>())!;
            response = await Post("/api/intake/agent", new AgentCommand(Guid.NewGuid(), state.Version, true));
            response.EnsureSuccessStatusCode();
            state = (await response.Content.ReadFromJsonAsync<IntakeView>())!;
        }
        var output = Environment.GetEnvironmentVariable("ONBOARDFLOW_EVALUATION_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, $"{scenario}-{repetition}.json"),
                JsonSerializer.Serialize(new { scenario, repetition, milliseconds = watch.ElapsedMilliseconds,
                    initial, final = state, sourceDocuments = specification.Documents, turns, responseShapes }, IntakeSpecification.Json));
        }
        Assert.True(state.Ai == (scenario == "conflict" ? "waiting" : "complete"),
            $"Expected a completed scenario, got {state.Ai}: {JsonSerializer.Serialize(state.Run, IntakeSpecification.Json)}");
        foreach (var id in new[] { "tax-return", "folder", "payment", "ach" }) Assert.False(state.Answers.ContainsKey(id));
        Assert.All(state.Answers.Values, answer =>
        {
            Assert.Equal("draft", answer.Status);
            Assert.Equal("ai", answer.Author);
            var source = answer.Evidence!;
            Assert.Equal(specification.Documents[source.Document].Sections.Single(x => x.Id == source.Section).Text, source.Quote);
        });
        Assert.Equal("Juniper Studio", state.Answers["dba"].Value);
        Assert.Equal("maya@juniper.example", state.Answers["email"].Value);
        Assert.Equal("2021-04-12", state.Answers["started"].Value);
        Assert.Contains("12/31", state.Answers["yearend"].Value);
        Assert.Contains("no", state.Answers["bank-two"].Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("No", state.Answers["loans"].Value);
        var card = state.Answers["credit-card"].Value;
        if (card.Contains("shared", StringComparison.OrdinalIgnoreCase))
            Assert.True(card.Contains("will", StringComparison.OrdinalIgnoreCase) || card.Contains("to be", StringComparison.OrdinalIgnoreCase)
                || card.Contains("plan", StringComparison.OrdinalIgnoreCase), "A future sharing plan must not be rewritten as completed.");
        var run = JsonSerializer.SerializeToElement(state.Run, IntakeSpecification.Json);
        Assert.InRange(run.GetProperty("calls").GetInt32(), 1, 6);
        if (scenario == "conflict")
        {
            Assert.Equal("waiting", state.Ai);
            Assert.False(state.Answers.ContainsKey("legal"));
            Assert.Equal("legal", run.GetProperty("question").GetProperty("fieldId").GetString());
            Assert.Equal(18, state.Answers.Count);
        }
        else
        {
            Assert.Equal("complete", state.Ai);
            Assert.Equal(19, state.Answers.Count);
            Assert.Equal(scenario == "missing" ? "DEMO-EIN-1042" : "DEMO-EIN-COMPLETE", state.Answers["tax"].Value);
            if (scenario == "missing") Assert.Equal("reply", state.Answers["tax"].Evidence!.Document);
        }
    }
}
