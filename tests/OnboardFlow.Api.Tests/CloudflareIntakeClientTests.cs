using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class CloudflareIntakeClientTests
{
    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public JsonObject? Payload { get; private set; }
        public Uri? Destination { get; private set; }
        public string? Credential { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Payload = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellation))!.AsObject();
            Destination = request.RequestUri;
            Credential = request.Headers.Authorization?.ToString();
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private static CloudflareIntakeClient Client(Handler handler, bool configured = true, string? model = CloudflareIntakeClient.PinnedModel) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(configured ? new Dictionary<string, string?>
        {
            ["Intake:Cloudflare:AccountId"] = new('a', 32),
            ["Intake:Cloudflare:ApiToken"] = "unit-test-placeholder", ["Intake:Cloudflare:Model"] = model
        } : []).Build());
    private static string Envelope(JsonObject result) => new JsonObject { ["success"] = true, ["result"] = result }.ToJsonString();
    private static JsonObject Result(JsonNode? content = null, int output = 100) => new()
    {
        ["response"] = content ?? JsonValue.Create("{\"answers\":[],\"question\":null,\"done\":true}"),
        ["usage"] = new JsonObject { ["prompt_tokens"] = 1000, ["completion_tokens"] = output }
    };
    private static readonly JsonObject[] Messages = [new() { ["role"] = "user", ["content"] = "Fill the fictional request." }];

    [Fact]
    public async Task Chat_transport_keeps_parallel_tool_ids_and_string_arguments_and_discards_hidden_reasoning()
    {
        var result = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject { ["finish_reason"] = "tool_calls", ["message"] = new JsonObject
            {
                ["reasoning_content"] = "not-for-evidence", ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = "provider-id", ["function"] = new JsonObject { ["name"] = "read_client_document", ["arguments"] = "{\"document\":\"reply\"}" }
                })
            } }),
            ["usage"] = new JsonObject { ["prompt_tokens"] = 1000, ["completion_tokens"] = 100 }
        };
        using var handler = new Handler(result.ToJsonString());
        JsonObject[] history =
        [
            new() { ["role"] = "assistant", ["tool_calls"] = new JsonArray(
                new JsonObject { ["function"] = new JsonObject { ["name"] = "read_required_fields", ["arguments"] = new JsonObject() } },
                new JsonObject { ["function"] = new JsonObject { ["name"] = "read_client_document", ["arguments"] = new JsonObject { ["document"] = "profile" } } }) },
            new() { ["role"] = "tool", ["content"] = "[]" }, new() { ["role"] = "tool", ["content"] = "{}" }
        ];
        var turn = await Client(handler, model: CloudflareIntakeClient.QwenModel).Turn(history, new(false, ["reply"]), default);
        Assert.Equal("api.cloudflare.com", handler.Destination!.Host);
        Assert.EndsWith("/ai/v1/chat/completions", handler.Destination.AbsolutePath);
        Assert.Equal("intake_0_0", handler.Payload!["messages"]![1]!["tool_call_id"]!.GetValue<string>());
        Assert.Equal("intake_0_1", handler.Payload["messages"]![2]!["tool_call_id"]!.GetValue<string>());
        Assert.False(handler.Payload["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());
        Assert.Equal("reply", turn.Message["tool_calls"]![0]!["function"]!["arguments"]!["document"]!.GetValue<string>());
        Assert.DoesNotContain("not-for-evidence", turn.Message.ToJsonString());
        Assert.Equal(70m - 0.0001m, turn.EstimatedNeurons);
    }
    [Fact]
    public async Task Observed_chat_quota_code_requires_the_allocation_message_and_preserves_reported_usage()
    {
        using var quota = new Handler("{\"success\":false,\"errors\":[{\"code\":4006,\"message\":\"You have used up your daily free allocation of 10,000 neurons\"}]}", HttpStatusCode.TooManyRequests);
        var error = await Assert.ThrowsAsync<IntakeException>(() => Client(quota, model: CloudflareIntakeClient.QwenModel).Turn(Messages, new(true, []), default));
        Assert.Equal(429, error.Status);
        Assert.Contains("free AI allowance", error.Message);
        using var other = new Handler("{\"success\":false,\"errors\":[{\"code\":4006,\"message\":\"A different inference error\"}]}", HttpStatusCode.TooManyRequests);
        Assert.Equal(503, (await Assert.ThrowsAsync<IntakeException>(() => Client(other).Turn(Messages, new(true, []), default))).Status);
        var result = Result(); result["usage"]!["neurons"] = 12.34m;
        using var metered = new Handler(Envelope(result));
        var turn = await Client(metered).Turn(Messages, new(false, []), default);
        Assert.Equal(12.34m, turn.ReportedNeurons);
        Assert.Equal(47.1485m, turn.EstimatedNeurons);
    }
    [Fact]
    public async Task Older_chat_compatible_models_normalize_native_response_envelopes()
    {
        var result = Result();
        result["tool_calls"] = new JsonArray(new JsonObject { ["name"] = "read_required_fields", ["arguments"] = new JsonObject() });
        using var handler = new Handler(result.ToJsonString());
        var turn = await Client(handler, model: CloudflareIntakeClient.FastQwenModel).Turn(Messages, new(true, []), default);
        Assert.Equal("read_required_fields", turn.Message["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal(7.6725m, turn.EstimatedNeurons);
    }
    [Fact]
    public async Task Unknown_or_paid_model_configuration_never_sends_a_request()
    {
        using var handler = new Handler("{}");
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler, model: "@cf/unapproved-model").Turn(Messages, new(true, []), default));
        Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task Uses_only_the_fixed_host_model_and_current_read_tools_with_output_bound()
    {
        var result = Result();
        result["tool_calls"] = new JsonArray(new JsonObject { ["name"] = "read_client_document", ["arguments"] = new JsonObject { ["document"] = "setup" } });
        using var handler = new Handler(Envelope(result));
        var turn = await Client(handler).Turn(Messages, new(false, ["setup"]), default);
        Assert.Equal("api.cloudflare.com", handler.Destination!.Host);
        Assert.Equal("https", handler.Destination.Scheme);
        Assert.EndsWith(CloudflareIntakeClient.PinnedModel, handler.Destination.AbsolutePath);
        Assert.Equal("Bearer unit-test-placeholder", handler.Credential);
        Assert.Equal(512, handler.Payload!["max_tokens"]!.GetValue<int>());
        Assert.False(handler.Payload["stream"]!.GetValue<bool>());
        var tools = handler.Payload["tools"]!.AsArray();
        Assert.Single(tools);
        Assert.Equal("read_client_document", tools[0]!["name"]!.GetValue<string>());
        Assert.Equal("[\"setup\"]", tools[0]!["parameters"]!["properties"]!["document"]!["enum"]!.ToJsonString());
        Assert.Equal("setup", turn.Message["tool_calls"]![0]!["function"]!["arguments"]!["document"]!.GetValue<string>());
        Assert.Equal(1000, turn.InputTokens);
        Assert.Equal(100, turn.Tokens);
        Assert.Equal(47.1485m, turn.EstimatedNeurons);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain("unit-test-placeholder", handler.Payload.ToJsonString());
    }
    [Fact]
    public async Task Completed_reads_use_schema_output_and_keep_tool_history_without_mutating_saved_messages()
    {
        using var handler = new Handler(Envelope(Result(new JsonObject { ["answers"] = new JsonArray(), ["question"] = null, ["done"] = true })));
        JsonObject[] history =
        [
            new() { ["role"] = "assistant", ["tool_calls"] = new JsonArray(new JsonObject { ["function"] = new JsonObject { ["name"] = "read_required_fields", ["arguments"] = new JsonObject() } }) },
            new() { ["role"] = "tool", ["tool_name"] = "read_required_fields", ["content"] = "[]" }
        ];
        var before = history[0].ToJsonString();
        var turn = await Client(handler).Turn(history, new(false, []), default);
        Assert.False(handler.Payload!.ContainsKey("tools"));
        Assert.Equal("json_schema", handler.Payload["response_format"]!["type"]!.GetValue<string>());
        Assert.Contains("read_required_fields", handler.Payload["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Equal(before, history[0].ToJsonString());
        Assert.True(JsonNode.Parse(turn.Message["content"]!.GetValue<string>())!["done"]!.GetValue<bool>());
    }
    [Theory]
    [InlineData(3036, 429, "free AI allowance")]
    [InlineData(3040, 503, "temporarily unavailable")]
    [InlineData(5035, 503, "No paid plan")]
    [InlineData(5018, 503, "No paid plan")]
    public async Task Quota_capacity_or_access_errors_stop_without_retry_or_returning_provider_secrets(int code, int status, string message)
    {
        using var handler = new Handler($"{{\"success\":false,\"errors\":[{{\"code\":{code},\"message\":\"private-provider-diagnostic\"}}]}}",
            code == 3036 || code == 3040 ? HttpStatusCode.TooManyRequests : HttpStatusCode.Forbidden);
        var error = await Assert.ThrowsAsync<IntakeException>(() => Client(handler).Turn(Messages, new(true, ["profile"]), default));
        Assert.Equal(status, error.Status);
        Assert.Contains(message, error.Message);
        Assert.DoesNotContain("private-provider-diagnostic", error.Message);
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task Unconfigured_credentials_do_not_contact_any_provider()
    {
        using var handler = new Handler("{}");
        var error = await Assert.ThrowsAsync<IntakeException>(() => Client(handler, false).Turn(Messages, new(true, []), default));
        Assert.Equal(503, error.Status);
        Assert.Equal(0, handler.Calls);
    }
    [Theory]
    [InlineData(512)] [InlineData(513)] [InlineData(-1)]
    public async Task Truncated_or_invalid_usage_never_becomes_an_accepted_draft(int output)
    {
        using var handler = new Handler(Envelope(Result(output: output)));
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler).Turn(Messages, new(false, []), default));
        Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"success\":true,\"result\":{\"response\":\"{}\"}}")]
    public async Task Malformed_or_unmetered_success_fails_closed(string body)
    {
        using var handler = new Handler(body);
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler).Turn(Messages, new(false, []), default));
    }
    [Fact]
    public async Task Oversized_input_is_rejected_before_inference_and_response_reads_are_bounded()
    {
        using var handler = new Handler(new string('x', 65537));
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler).Turn(
            [new() { ["role"] = "user", ["content"] = new string('x', 65537) }], new(false, []), default));
        Assert.Equal(0, handler.Calls);
        await Assert.ThrowsAsync<IntakeException>(() => Client(handler).Turn(Messages, new(false, []), default));
        Assert.Equal(1, handler.Calls);
    }
}
