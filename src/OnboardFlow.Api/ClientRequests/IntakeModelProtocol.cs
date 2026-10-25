using System.Text.Json.Nodes;

namespace OnboardFlow.Api;

internal static class IntakeModelProtocol
{
    internal static JsonArray AvailableTools(IntakeToolAccess access)
    {
        // Each turn gets its own schema; adapter-specific filtering must not mutate another call.
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
