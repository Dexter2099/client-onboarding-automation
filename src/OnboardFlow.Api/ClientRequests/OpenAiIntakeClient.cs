using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OnboardFlow.Api;

public sealed class OpenAiIntakeClient(HttpClient http, IConfiguration configuration, IIntakeSpendBudget budget) : IIntakeModelClient
{
    public const string PinnedModel = "gpt-5.4-2026-03-05";
    public string Model => PinnedModel;
    internal static long CostMicros(int input, int output) => checked((long)decimal.Ceiling(input * 2.50m + output * 15m));
    public async Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation)
    {
        var key = configuration["Intake:OpenAI:ApiKey"] ?? "";
        var project = configuration["Intake:OpenAI:ProjectId"] ?? "";
        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace) || !Regex.IsMatch(project, "\\Aproj_[a-zA-Z0-9]+\\z")
            || !configuration.GetValue<bool>("Intake:OpenAI:BudgetVerified"))
            throw new IntakeException(503, "Hosted AI setup or spending-limit verification is incomplete. Manual completion is available.");
        var tools = new JsonArray();
        foreach (var entry in IntakeModelProtocol.AvailableTools(access))
        {
            var tool = entry!["function"]!.DeepClone().AsObject();
            tool["type"] = "function"; tool["strict"] = true;
            tool["parameters"]!["required"] ??= new JsonArray();
            tools.Add(tool);
        }
        var payload = new JsonObject
        {
            ["model"] = Model, ["input"] = History(messages),
            ["reasoning"] = new JsonObject { ["effort"] = "none" }
        };
        if (tools.Count > 0) payload["tools"] = tools;
        else payload["text"] = new JsonObject
        {
            ["format"] = new JsonObject { ["type"] = "json_schema", ["name"] = "intake_drafts",
                ["strict"] = true, ["schema"] = JsonNode.Parse(IntakeModelProtocol.ResultSchema) }
        };
        if (Encoding.UTF8.GetByteCount(payload.ToJsonString()) > 65536)
            throw new IntakeException(422, "The AI input reached its size limit. Saved work is available.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var watch = Stopwatch.StartNew();
        // Count the same input/tools/schema. No local characters-to-tokens estimate.
        var count = await Send("responses/input_tokens", payload, key, project, deadline.Token);
        var inputCount = count["input_tokens"]?.GetValue<int>() ?? -1;
        if (inputCount is < 1 or > 8192) throw new IntakeException(422, "The AI input reached its token limit.");
        // Buffer formatting-token drift as well as the full 512-token output before sending.
        var reservation = await budget.Reserve(project, CostMicros(inputCount + 256, 512), deadline.Token);
        payload["max_output_tokens"] = 512;
        payload["temperature"] = 0;
        payload["store"] = false; payload["stream"] = false;
        payload["service_tier"] = "default";
        payload["parallel_tool_calls"] = true;
        var result = await Send("responses", payload, key, project, deadline.Token);
        var input = result["usage"]?["input_tokens"]?.GetValue<int>() ?? -1;
        var output = result["usage"]?["output_tokens"]?.GetValue<int>() ?? -1;
        if (input < 0 || output < 0) throw new IntakeException(422, "The model omitted valid usage. Its reservation has been retained.");
        // Settle even truncated/invalid answers; these generated tokens can still be billed.
        var cost = CostMicros(input, output);
        await budget.Settle(project, reservation, cost, CancellationToken.None);
        if (result["status"]?.GetValue<string>() != "completed" || output >= 512)
            throw new IntakeException(422, "The model did not finish within its output limit. Saved work is available.");
        var items = result["output"] as JsonArray ?? throw new IntakeException(422, "The model omitted its output.");
        var message = new JsonObject { ["role"] = "assistant", ["content"] = "" };
        var calls = new JsonArray();
        var texts = new List<string>();
        foreach (var item in items)
        {
            var type = item?["type"]?.GetValue<string>();
            if (type == "function_call")
                calls.Add(new JsonObject { ["function"] = new JsonObject
                {
                    ["name"] = item!["name"]?.DeepClone(),
                    ["arguments"] = JsonNode.Parse(item["arguments"]!.GetValue<string>())
                } });
            else if (type == "message")
                foreach (var content in item!["content"]!.AsArray())
                    if (content?["type"]?.GetValue<string>() == "output_text") texts.Add(content["text"]!.GetValue<string>());
                    else throw new IntakeException(422, "The model declined to return a structured answer.");
            else if (type != "reasoning") throw new IntakeException(422, "The model returned an unsupported output type.");
        }
        if (calls.Count > 3) throw new IntakeException(422, "Too many tool requests.");
        if (calls.Count > 0) message["tool_calls"] = calls;
        else message["content"] = string.Join("", texts);
        return new(message, output, watch.ElapsedMilliseconds, input, AccountedUsd: cost / 1_000_000m);
    }
    private async Task<JsonObject> Send(string path, JsonObject payload, string key, string project, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("OpenAI-Project", project);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var bytes = new byte[65537]; var length = 0;
        while (length < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(length), cancellation);
            if (read == 0) break;
            length += read;
        }
        if (length > 65536) throw new IntakeException(422, "The model response was too large.");
        if (!response.IsSuccessStatusCode)
            throw new IntakeException(503, response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                ? "Hosted AI is unavailable because of a provider usage or spending limit. Saved work is available."
                : "Hosted AI is temporarily unavailable. No request was repeated or sent to another provider.");
        return JsonNode.Parse(bytes.AsSpan(0, length))?.AsObject() ?? throw new IntakeException(422, "The model response was invalid.");
    }
    private static JsonArray History(IReadOnlyList<JsonObject> messages)
    {
        var result = new JsonArray(); var pending = new Queue<string>();
        for (var i = 0; i < messages.Count; i++)
        {
            var source = messages[i];
            if (source["tool_calls"] is JsonArray calls && calls.Count > 0)
            {
                for (var j = 0; j < calls.Count; j++)
                {
                    var id = $"intake_{i}_{j}"; pending.Enqueue(id);
                    var function = calls[j]!["function"]!;
                    result.Add(new JsonObject { ["type"] = "function_call", ["call_id"] = id,
                        ["name"] = function["name"]!.DeepClone(), ["arguments"] = function["arguments"]!.ToJsonString() });
                }
            }
            else if (source["role"]!.GetValue<string>() == "tool")
                result.Add(new JsonObject { ["type"] = "function_call_output", ["call_id"] = pending.Dequeue(), ["output"] = source["content"]!.DeepClone() });
            else result.Add(new JsonObject { ["role"] = source["role"]!.DeepClone(), ["content"] = source["content"]!.DeepClone() });
        }
        return result;
    }
}
