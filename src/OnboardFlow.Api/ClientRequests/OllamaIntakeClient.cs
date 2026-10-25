using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OnboardFlow.Api;

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
