using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class ClientRequestRecord
{
    public Guid Id { get; set; }
    public Guid SandboxSessionId { get; set; }
    public long Version { get; set; }
    public string StateJson { get; set; } = "{}";
}

public sealed partial class FoundationDb
{
    public DbSet<ClientRequestRecord> ClientRequests => Set<ClientRequestRecord>();
    private static void ConfigureIntake(ModelBuilder model)
    {
        var request = model.Entity<ClientRequestRecord>();
        request.HasIndex(x => x.SandboxSessionId).IsUnique();
        request.HasOne<SandboxSession>().WithOne().HasForeignKey<ClientRequestRecord>(x => x.SandboxSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        request.Property(x => x.Version).IsConcurrencyToken();
        request.Property(x => x.StateJson).HasColumnType("jsonb");
    }
}

public sealed record IntakeEvidence(string Document, string Section, string Quote, int Version = 1);
public sealed record IntakeAnswer(string Value, string Status, string Author, IntakeEvidence? Evidence = null, int Revision = 1);
public sealed record IntakeQuestion(string FieldId, string Text);
public sealed record IntakeTrace(string Action, string Detail, int Tokens = 0, long Milliseconds = 0,
    int InputTokens = 0, decimal? EstimatedNeurons = null, decimal? ReportedNeurons = null, decimal? AccountedUsd = null);
public sealed record IntakeReceipt(Guid CommandId, string Fingerprint);
public sealed class IntakeRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommandId { get; set; }
    public string Status { get; set; } = "filling";
    public int Calls { get; set; }
    public Guid Claim { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "intake-v1";
    public List<JsonObject> Messages { get; set; } = [];
    public HashSet<string> ReadDocuments { get; set; } = [];
    public bool FieldsRead { get; set; }
    public List<IntakeTrace> Trace { get; set; } = [];
    public IntakeQuestion? Question { get; set; }
    public string? Error { get; set; }
}
public sealed class IntakeState
{
    public Dictionary<string, IntakeAnswer> Answers { get; set; } = [];
    public Dictionary<string, string> Questions { get; set; } = [];
    public bool ReplyReady { get; set; }
    public int RunsStarted { get; set; }
    public IntakeRun? Run { get; set; }
    public List<IntakeReceipt> Receipts { get; set; } = [];
    public string SpecificationVersion { get; set; } = "";
}
public sealed record IntakeCommand(Guid CommandId, long ExpectedVersion, string Operation,
    string? Field = null, string? Value = null, string? Page = null);
public sealed record AgentCommand(Guid CommandId, long ExpectedVersion, bool Resume = false);
public sealed record IntakeView(long Version, Dictionary<string, IntakeAnswer> Answers, Dictionary<string, string> Questions,
    bool ReplyReady, string Ai, object? Run, string Model);
public sealed record IntakeField(string Id, string Label, string Page, string Group, string Kind,
    string[]? Options, string? Sample, string[]? Confirmation, string[] AllowedSources, string? Hint = null)
{
    public bool Manual => Kind is "attachment" or "confirmation";
}
public sealed record IntakeSection(string Id, string Title, string Text);
public sealed record IntakeDocument(string Name, int Page, IntakeSection[] Sections);

public sealed class IntakeSpecification
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public IntakeField[] Fields { get; }
    public Dictionary<string, IntakeDocument> Documents { get; }
    public string Hash { get; }
    public IntakeSpecification()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ClientRequests", "demo.json"));
        Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        using var json = JsonDocument.Parse(text);
        Fields = json.RootElement.GetProperty("fields").Deserialize<IntakeField[]>(Json)!;
        Documents = json.RootElement.GetProperty("documents").Deserialize<Dictionary<string, IntakeDocument>>(Json)!;
    }
}

public sealed class IntakeException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
