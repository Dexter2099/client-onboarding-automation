using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OnboardFlow.Api;

/// <summary>Direct Workers AI REST adapter. No Worker, SDK, retry or alternate provider.</summary>
public sealed class CloudflareIntakeClient(HttpClient http, IConfiguration configuration) : IIntakeModelClient
{
    // Explicit evaluation candidates only. Never switch models after a failed request.
    public const string PinnedModel = "@cf/meta/llama-3.3-70b-instruct-fp8-fast";
    public const string QwenModel = "@cf/qwen/qwen3.8-27b";
    public const string FastQwenModel = "@cf/qwen/qwen3-30b-a3b-fp8";
    public string Model => configuration["Intake:Cloudflare:Model"] ?? QwenModel;
    public async Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation)
    {
        var account = configuration["Intake:Cloudflare:AccountId"] ?? "";
        var token = configuration["Intake:Cloudflare:ApiToken"] ?? "";
        if (!Regex.IsMatch(account, "\\A[0-9a-fA-F]{32}\\z") || string.IsNullOrWhiteSpace(token)
            || token.Any(char.IsWhiteSpace))
            throw new IntakeException(503, "Hosted AI is not configured. You can still complete the request manually.");
        if (Model is not (PinnedModel or QwenModel or FastQwenModel))
            throw new IntakeException(503, "The hosted model is not an allowed evaluation candidate. No alternate model was used.");
        var chat = Model != PinnedModel;

        var available = IntakeModelProtocol.AvailableTools(access);
        var tools = chat ? available : new JsonArray(available.Select(x => x!["function"]!.DeepClone()).ToArray());
        var history = chat ? ChatHistory(messages) : new JsonArray(messages.Select(ToProviderMessage).ToArray());
        if (Model == FastQwenModel)
        {
            // Qwen3's documented soft switch complements the provider template flag.
            // Never log or reinterpret reasoning as a tool/result if the provider ignores it.
            var last = history.Last()!.AsObject();
            last["content"] = (last["content"]?.GetValue<string>() ?? "") + "\n/no_think";
        }
        var payload = new JsonObject
        {
            ["messages"] = history, ["stream"] = false, ["temperature"] = 0, ["max_tokens"] = 512
        };
        if (chat)
        {
            payload["model"] = Model;
            payload["store"] = false;
            payload["parallel_tool_calls"] = true;
            payload["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false };
        }
        if (tools.Count > 0) payload["tools"] = tools;
        else payload["response_format"] = new JsonObject
        {
            ["type"] = "json_schema", ["json_schema"] = chat ? new JsonObject
            {
                ["name"] = "intake_drafts", ["strict"] = true, ["schema"] = JsonNode.Parse(IntakeModelProtocol.ResultSchema)
            } : JsonNode.Parse(IntakeModelProtocol.ResultSchema)
        };
        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
        if (body.Length > 65536) throw new IntakeException(422, "The AI input reached its size limit. Saved work is available.");
        using var request = new HttpRequestMessage(HttpMethod.Post, chat
            ? $"https://api.cloudflare.com/client/v4/accounts/{account}/ai/v1/chat/completions"
            : $"https://api.cloudflare.com/client/v4/accounts/{account}/ai/run/{PinnedModel}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var watch = Stopwatch.StartNew();
        // The application reserves a call before this method; uncertainty is never retried/refunded.
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        var bytes = new byte[65537];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token);
            if (read == 0) break;
            length += read;
        }
        if (length > 65536) throw new IntakeException(422, "The model response was too large.");
        JsonObject envelope;
        try { envelope = JsonNode.Parse(bytes.AsSpan(0, length))!.AsObject(); }
        catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or NullReferenceException)
        { throw new IntakeException(503, "Hosted AI returned an unreadable response. Saved work is available."); }
        var codes = (envelope["errors"] as JsonArray)?.Select(x => x?["code"]?.GetValue<int>()).ToArray() ?? [];
        // REST documented code 3036; this account's chat endpoint also returns 4006
        // with the explicit free-allocation message. Other 4006 errors are not quota proof.
        var exhausted = (envelope["errors"] as JsonArray)?.Any(x => x?["code"]?.GetValue<int>() == 4006
            && (x?["message"]?.GetValue<string>() ?? "").Contains("daily free allocation", StringComparison.OrdinalIgnoreCase)) == true;
        if (codes.Contains(3036) || exhausted)
            throw new IntakeException(429, "Today's free AI allowance has been used. You can complete the request manually or watch the labelled recorded walkthrough.");
        if (codes.Contains(5035) || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new IntakeException(503, "Hosted AI access is unavailable. No paid plan or alternate provider was used.");
        if (!response.IsSuccessStatusCode || envelope["success"]?.GetValue<bool>() == false
            || (!chat && envelope["success"]?.GetValue<bool>() != true)
            || (chat && envelope.ContainsKey("error")))
            throw new IntakeException(503, "Hosted AI is temporarily unavailable. Saved drafts are available; no request was repeated.");
        var result = envelope["result"] as JsonObject ?? envelope;
        var usage = result["usage"] as JsonObject ?? throw new IntakeException(422, "The model omitted its usage information.");
        var inputTokens = usage["prompt_tokens"]?.GetValue<int>() ?? -1;
        var outputTokens = usage["completion_tokens"]?.GetValue<int>() ?? -1;
        var choice = result["choices"] is JsonArray choices ? choices.FirstOrDefault() as JsonObject
            ?? throw new IntakeException(422, "The model omitted its result.") : null;
        var generated = choice is not null ? choice["message"] as JsonObject
            ?? throw new IntakeException(422, "The model omitted its result.") : result;
        if (inputTokens < 0 || outputTokens is < 0 or >= 512 || (choice ?? result)["finish_reason"]?.GetValue<string>() == "length")
            throw new IntakeException(422, "The model reached its output limit or returned invalid usage. Saved work is available.");
        var message = new JsonObject { ["role"] = "assistant", ["content"] = "" };
        if (generated["tool_calls"] is JsonArray calls && calls.Count > 0)
        {
            if (calls.Count > 3) throw new IntakeException(422, "Too many tool requests.");
            message["tool_calls"] = new JsonArray(calls.Select(call =>
            {
                var function = call?["function"] ?? call ?? throw new IntakeException(422, "The model returned an invalid tool request.");
                var arguments = function["arguments"];
                if (arguments is JsonValue value && value.TryGetValue<string>(out var argumentsText))
                    arguments = JsonNode.Parse(argumentsText);
                return (JsonNode)new JsonObject
                {
                    ["function"] = new JsonObject { ["name"] = function["name"]?.DeepClone(), ["arguments"] = arguments?.DeepClone() }
                };
            }).ToArray());
        }
        else
        {
            var content = generated[choice is not null ? "content" : "response"];
            message["content"] = content is JsonValue value && value.TryGetValue<string>(out var text)
                ? text : content?.ToJsonString() ?? throw new IntakeException(422, "The model omitted its result.");
        }
        // Published per-million rates, 25 October 2026. Estimate only; dashboard is billing authority.
        var neurons = Model switch
        {
            QwenModel => (inputTokens * 40909m + outputTokens * 290909m) / 1_000_000m,
            FastQwenModel => (inputTokens * 4625m + outputTokens * 30475m) / 1_000_000m,
            _ => (inputTokens * 26668m + outputTokens * 204805m) / 1_000_000m
        };
        var reportedNeurons = usage["neurons"] is JsonValue reported && reported.TryGetValue<decimal>(out var amount) && amount >= 0
            ? (decimal?)amount : null;
        return new(message, outputTokens, watch.ElapsedMilliseconds, inputTokens, neurons, reportedNeurons);
    }
    private static JsonArray ChatHistory(IReadOnlyList<JsonObject> messages)
    {
        var history = new JsonArray();
        var pending = new Queue<string>();
        for (var i = 0; i < messages.Count; i++)
        {
            var source = messages[i];
            var message = new JsonObject { ["role"] = source["role"]!.DeepClone(), ["content"] = source["content"]?.DeepClone() ?? JsonValue.Create("") };
            if (source["tool_calls"] is JsonArray calls && calls.Count > 0)
            {
                var translated = new JsonArray();
                for (var j = 0; j < calls.Count; j++)
                {
                    var id = $"intake_{i}_{j}";
                    var function = calls[j]!["function"]!;
                    pending.Enqueue(id);
                    translated.Add(new JsonObject
                    {
                        ["id"] = id, ["type"] = "function", ["function"] = new JsonObject
                        {
                            ["name"] = function["name"]!.DeepClone(), ["arguments"] = function["arguments"]!.ToJsonString()
                        }
                    });
                }
                message["tool_calls"] = translated;
            }
            if (source["role"]!.GetValue<string>() == "tool")
                message["tool_call_id"] = pending.Dequeue();
            history.Add(message);
        }
        return history;
    }
    private static JsonNode ToProviderMessage(JsonObject source)
    {
        var message = new JsonObject { ["role"] = source["role"]!.DeepClone(), ["content"] = source["content"]?.DeepClone() ?? JsonValue.Create("") };
        if (source["tool_calls"] is JsonArray calls && calls.Count > 0)
            message["content"] = new JsonArray(calls.Select(x => x!["function"]!.DeepClone()).ToArray()).ToJsonString();
        return message;
    }
}
