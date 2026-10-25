using System.Text.Json.Nodes;

namespace OnboardFlow.Api;

public sealed record IntakeModelTurn(JsonObject Message, int Tokens, long Milliseconds,
    int InputTokens = 0, decimal? EstimatedNeurons = null, decimal? ReportedNeurons = null, decimal? AccountedUsd = null);
public sealed record IntakeToolAccess(bool FieldsAvailable, string[] Documents);
/// <summary>Provider-neutral turn boundary. The agent owns source validation and run limits.</summary>
public interface IIntakeModelClient
{
    string Model { get; }
    Task<IntakeModelTurn> Turn(IReadOnlyList<JsonObject> messages, IntakeToolAccess access, CancellationToken cancellation);
}
