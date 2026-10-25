using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OnboardFlow.Api;

public sealed record IntakeModelTurn(JsonObject Message, int Tokens, long Milliseconds,
    int InputTokens = 0, decimal? EstimatedNeurons = null, decimal? ReportedNeurons = null, decimal? AccountedUsd = null);
public sealed record IntakeToolAccess(bool FieldsAvailable, string[] Documents);
public interface IIntakeModelClient
{
    string Model { get; }
    Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation);
}
public sealed class OllamaIntakeClient(HttpClient http) : IIntakeModelClient
{
    public string Model => "qwen3.8:27b";
    public async Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation)
    {
        var watch = Stopwatch.StartNew();
        var tools = IntakeModelProtocol.AvailableTools(access);
        var payload = new
        {
            model = Model, messages, stream = false, think = false, keep_alive = "30m",
            options = new { temperature = 0, num_ctx = 8192, num_predict = 512 },
            tools,
            format = tools.Count == 0 ? JsonNode.Parse(IntakeModelProtocol.ResultSchema) : null
        };
        using var response = await http.PostAsJsonAsync("api/chat", payload, cancellation);
        if (!response.IsSuccessStatusCode) throw new IntakeException(503, "Local AI is unavailable. Check Ollama, then load saved work.");
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellation);
        if (bytes.Length > 65536) throw new IntakeException(422, "The model response was too large.");
        var json = JsonNode.Parse(bytes)!.AsObject();
        if (json["done_reason"]?.GetValue<string>() == "length")
            throw new IntakeException(422, "The model reached its output limit. Saved work is unchanged.");
        var message = json["message"]?.DeepClone().AsObject() ?? throw new IntakeException(422, "The model response was invalid.");
        message.Remove("thinking");
        return new(message, json["eval_count"]?.GetValue<int>() ?? 0, watch.ElapsedMilliseconds,
            json["prompt_eval_count"]?.GetValue<int>() ?? 0);
    }
}

internal static class IntakeModelProtocol
{
    internal static JsonArray AvailableTools(IntakeToolAccess access)
    {
        var tools = JsonNode.Parse(Tools)!.AsArray();
        if (access.Documents.Length > 0)
        {
            tools[1]!["function"]!["parameters"]!["properties"]!["document"]!["enum"] =
                new JsonArray(access.Documents.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
            tools[1]!["function"]!["description"] = "Read an unread available client document: " + string.Join(", ", access.Documents);
        }
        else tools.RemoveAt(1);
        if (!access.FieldsAvailable) tools.RemoveAt(0);
        return tools;
    }
    private const string Tools = """
    [
      {"type":"function","function":{"name":"read_required_fields","description":"Read all required field IDs, types, choices, source constraints and manual-only items.","parameters":{"type":"object","properties":{},"additionalProperties":false}}},
      {"type":"function","function":{"name":"read_client_document","description":"Read client source data. Available documents: profile, setup; reply only after the client supplies it.","parameters":{"type":"object","properties":{"document":{"type":"string","enum":["profile","setup","reply"]}},"required":["document"],"additionalProperties":false}}}
    ]
    """;
    internal const string ResultSchema = """
    {"type":"object","additionalProperties":false,"required":["answers","question","done"],"properties":{
      "answers":{"type":"array","maxItems":8,"items":{"type":"array","minItems":4,"maxItems":4,"items":{"type":"string"}}},
      "question":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"required":["fieldId","text"],"properties":{"fieldId":{"type":"string"},"text":{"type":"string"}}}]},
      "done":{"type":"boolean"}
    }}
    """;
}
