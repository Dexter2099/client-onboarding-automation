using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class OpenAiIntakeClientTests
{
    private sealed class Budget(List<string> events) : IIntakeSpendBudget
    {
        public long Reserved; public long? Settled; public bool Deny;
        public Task<Guid> Reserve(string scope, long maximumMicros, CancellationToken cancellation)
        {
            events.Add("reserve");
            if (Deny) throw new IntakeException(429, "Monthly allowance used.");
            Reserved = maximumMicros; return Task.FromResult(Guid.NewGuid());
        }
        public Task Settle(string scope, Guid reservation, long costMicros, CancellationToken cancellation)
        { events.Add("settle"); Settled = costMicros; return Task.CompletedTask; }
    }
    private sealed class Handler(List<string> events, string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int CountTokens = 1000;
        public List<JsonObject> Payloads = [];
        public List<Uri> Destinations = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal("api.openai.com", request.RequestUri!.Host);
            Assert.Equal("https", request.RequestUri.Scheme);
            Assert.Equal("Bearer unit-test-placeholder", request.Headers.Authorization!.ToString());
            Assert.Equal("proj_test", request.Headers.GetValues("OpenAI-Project").Single());
            var counting = request.RequestUri.AbsolutePath.EndsWith("/input_tokens");
            events.Add(counting ? "count" : "infer");
            Payloads.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!.AsObject());
            Destinations.Add(request.RequestUri);
            return new(counting ? HttpStatusCode.OK : status)
            { Content = new StringContent(counting ? $"{{\"input_tokens\":{CountTokens}}}" : response, Encoding.UTF8, "application/json") };
        }
    }
    private static string Output(string status = "completed", int tokens = 100, JsonArray? output = null) => new JsonObject
    {
        ["status"] = status, ["usage"] = new JsonObject { ["input_tokens"] = 1000, ["output_tokens"] = tokens },
        ["output"] = output ?? new JsonArray(new JsonObject { ["type"] = "message", ["content"] = new JsonArray(new JsonObject
        { ["type"] = "output_text", ["text"] = "{\"answers\":[],\"question\":null,\"done\":true}" }) })
    }.ToJsonString();
    private static OpenAiIntakeClient Client(Handler handler, Budget budget, bool verified = true) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Intake:OpenAI:ApiKey"] = "unit-test-placeholder", ["Intake:OpenAI:ProjectId"] = "proj_test",
            ["Intake:OpenAI:BudgetVerified"] = verified.ToString()
        }).Build(), budget);
    private static readonly JsonObject[] Messages = [new() { ["role"] = "user", ["content"] = "Fill the fictional request." }];
    [Fact]
    public async Task Counts_the_actual_payload_reserves_before_inference_and_settles_reported_usage()
    {
        var events = new List<string>(); var budget = new Budget(events); using var handler = new Handler(events, Output());
        var turn = await Client(handler, budget).Turn(Messages, new(false, []), default);
        Assert.Equal(new[] { "count", "reserve", "infer", "settle" }, events);
        Assert.Equal(OpenAiIntakeClient.CostMicros(1256, 512), budget.Reserved);
        Assert.Equal(4000, budget.Settled);
        Assert.Equal(0.004m, turn.AccountedUsd);
        Assert.True(JsonNode.DeepEquals(handler.Payloads[0]["input"], handler.Payloads[1]["input"]));
        Assert.True(JsonNode.DeepEquals(handler.Payloads[0]["text"], handler.Payloads[1]["text"]));
        Assert.Equal(512, handler.Payloads[1]["max_output_tokens"]!.GetValue<int>());
        Assert.False(handler.Payloads[0].ContainsKey("temperature"));
        Assert.Equal(0, handler.Payloads[1]["temperature"]!.GetValue<int>());
        Assert.False(handler.Payloads[1]["store"]!.GetValue<bool>());
        Assert.Equal("default", handler.Payloads[1]["service_tier"]!.GetValue<string>());
        Assert.Equal(OpenAiIntakeClient.PinnedModel, handler.Payloads[1]["model"]!.GetValue<string>());
        Assert.DoesNotContain("unit-test-placeholder", handler.Payloads[1].ToJsonString());
    }
    [Fact]
    public async Task Budget_denial_or_unverified_settings_never_generate_paid_tokens()
    {
        var events = new List<string>(); var budget = new Budget(events) { Deny = true }; using var handler = new Handler(events, Output());
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler, budget, false).Turn(Messages, new(true, []), default));
        Assert.Empty(events);
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler, budget).Turn(Messages, new(true, []), default));
        Assert.Equal(new[] { "count", "reserve" }, events);
    }
    [Theory]
    [InlineData(0)] [InlineData(8193)]
    public async Task Invalid_or_oversized_input_counts_never_generate(int count)
    {
        var events = new List<string>(); var budget = new Budget(events); using var handler = new Handler(events, Output()) { CountTokens = count };
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler, budget).Turn(Messages, new(true, []), default));
        Assert.Equal(new[] { "count" }, events);
    }
    [Theory]
    [InlineData("incomplete", 512)] [InlineData("completed", 512)]
    public async Task Incomplete_output_is_accounted_before_rejection(string status, int tokens)
    {
        var events = new List<string>(); var budget = new Budget(events); using var handler = new Handler(events, Output(status, tokens));
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler, budget).Turn(Messages, new(false, []), default));
        Assert.NotNull(budget.Settled);
        Assert.Equal(new[] { "count", "reserve", "infer", "settle" }, events);
    }
    [Fact]
    public async Task Provider_failure_keeps_the_reservation_without_retry_or_fallback()
    {
        var events = new List<string>(); var budget = new Budget(events); using var handler = new Handler(events, "private-provider-error", HttpStatusCode.TooManyRequests);
        var error = await Assert.ThrowsAsync<IntakeException>(() => Client(handler, budget).Turn(Messages, new(false, []), default));
        Assert.Equal(new[] { "count", "reserve", "infer" }, events);
        Assert.Null(budget.Settled);
        Assert.DoesNotContain("private-provider-error", error.Message);
    }
    [Fact]
    public async Task Read_tools_and_multi_call_history_use_responses_format_and_ignore_hidden_reasoning()
    {
        var events = new List<string>(); var budget = new Budget(events);
        var output = new JsonArray(new JsonObject { ["type"] = "reasoning", ["summary"] = "not-for-evidence" }, new JsonObject
        { ["type"] = "function_call", ["name"] = "read_client_document", ["arguments"] = "{\"document\":\"reply\"}" });
        using var handler = new Handler(events, Output(output: output));
        JsonObject[] history =
        [
            new() { ["role"] = "assistant", ["tool_calls"] = new JsonArray(new JsonObject
            { ["function"] = new JsonObject { ["name"] = "read_required_fields", ["arguments"] = new JsonObject() } }) },
            new() { ["role"] = "tool", ["content"] = "[]" }
        ];
        var turn = await Client(handler, budget).Turn(history, new(false, ["reply"]), default);
        var payload = handler.Payloads[1];
        Assert.Equal("function_call", payload["input"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("function_call_output", payload["input"]![1]!["type"]!.GetValue<string>());
        Assert.Equal(payload["input"]![0]!["call_id"]!.GetValue<string>(), payload["input"]![1]!["call_id"]!.GetValue<string>());
        Assert.Single(payload["tools"]!.AsArray());
        Assert.Equal("read_client_document", payload["tools"]![0]!["name"]!.GetValue<string>());
        Assert.True(payload["tools"]![0]!["strict"]!.GetValue<bool>());
        Assert.Equal("reply", turn.Message["tool_calls"]![0]!["function"]!["arguments"]!["document"]!.GetValue<string>());
        Assert.DoesNotContain("not-for-evidence", turn.Message.ToJsonString());
    }
}
