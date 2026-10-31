using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class IntakeStore(FoundationDb db, TimeProvider clock, IntakeSpecification spec)
{
    public static IntakeView View(IntakeSnapshot snapshot)
    {
        var state = snapshot.State;
        var run = state.Run;
        return new(snapshot.Version, state.Answers, state.Questions, state.ReplyReady, run?.Status ?? "idle",
            run is null ? null : new { run.Id, run.Model, run.Status, run.Calls, run.Question, run.Error, run.Trace, run.PromptVersion,
                schemaVersion = "draft-array-v1", sourceVersion = state.SpecificationVersion },
            run?.Model ?? "");
    }
    public async Task<IntakeSnapshot> Update(string? token, Action<IntakeState, long> change, CancellationToken cancellation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var session = await SandboxSessionAccess.Find(token, db, true, cancellation);
        if (!SandboxSessionAccess.Active(session, clock)) throw new IntakeException(401, "This sandbox expired. Start a fresh workspace.");
        var record = await db.ClientRequests.SingleOrDefaultAsync(x => x.SandboxSessionId == session!.Id, cancellation);
        if (record is null)
        {
            record = new() { Id = Guid.NewGuid(), SandboxSessionId = session!.Id };
            db.ClientRequests.Add(record);
        }
        var state = JsonSerializer.Deserialize<IntakeState>(record.StateJson, IntakeSpecification.Json)!;
        if (state.SpecificationVersion != "" && state.SpecificationVersion != spec.Hash)
        {
            foreach (var id in state.Answers.Where(x => x.Value.Author == "ai").Select(x => x.Key).ToArray())
                state.Answers.Remove(id);
            if (state.Run is not null)
            {
                state.Run.Status = "failed";
                state.Run.Error = "The source documents changed. Old AI drafts were invalidated; start a fresh review.";
            }
        }
        state.SpecificationVersion = spec.Hash;
        if (state.Run is { Status: "filling" } running && clock.GetUtcNow() >= running.Deadline)
        {
            running.Status = "interrupted";
            running.Error = "The review was interrupted. Saved drafts are available; no model call was repeated.";
        }
        change(state, record.Version);
        var json = JsonSerializer.Serialize(state, IntakeSpecification.Json);
        if (!System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(json), System.Text.Json.Nodes.JsonNode.Parse(record.StateJson)))
        {
            record.StateJson = json;
            record.Version++;
        }
        if (!SandboxSessionAccess.Active(session, clock)) throw new IntakeException(401, "This sandbox expired.");
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return new(record.Version, state);
    }
    public async Task<IntakeSnapshot> Read(string? token, CancellationToken cancellation)
    {
        var session = await SandboxSessionAccess.Find(token, db, false, cancellation);
        if (!SandboxSessionAccess.Active(session, clock)) throw new IntakeException(401, "This sandbox expired. Start a fresh workspace.");
        var record = await db.ClientRequests.AsNoTracking().SingleOrDefaultAsync(x => x.SandboxSessionId == session!.Id, cancellation);
        if (record is null) return await Update(token, (_, _) => { }, cancellation);
        var state = JsonSerializer.Deserialize<IntakeState>(record.StateJson, IntakeSpecification.Json)!;
        // Only initialization, changed sources or expired runs need the existing locked transition.
        if (state.SpecificationVersion != spec.Hash || state.Run is { Status: "filling" } run && clock.GetUtcNow() >= run.Deadline)
            return await Update(token, (_, _) => { }, cancellation);
        if (!SandboxSessionAccess.Active(session, clock)) throw new IntakeException(401, "This sandbox expired.");
        return new(record.Version, state);
    }
    public Task<IntakeSnapshot> Command(string? token, IntakeCommand command, CancellationToken cancellation) =>
        Update(token, (state, version) =>
        {
            var fingerprint = Fingerprint(command);
            var prior = state.Receipts.SingleOrDefault(x => x.CommandId == command.CommandId);
            if (prior is not null)
            {
                if (prior.Fingerprint != fingerprint) throw new IntakeException(409, "Command identity was reused with different input.");
                return;
            }
            // Explicit reset discards current work, including a run that advanced since the last poll.
            var reset = command.Operation == "reset" && command.ExpectedVersion >= 0 && command.ExpectedVersion <= version;
            if (command.CommandId == Guid.Empty || (!reset && command.ExpectedVersion != version))
                throw new IntakeException(409, "The request changed. Load saved work before continuing.");
            if (state.Run?.Status == "filling" && !reset) throw new IntakeException(409, "Wait for the active AI review.");
            var field = spec.Fields.SingleOrDefault(x => x.Id == command.Field);
            switch (command.Operation)
            {
                case "edit":
                    if (field is null || command.Value is null || command.Value.Length > 2000)
                        throw new IntakeException(400, "Unknown field or oversized answer.");
                    var revision = state.Answers.GetValueOrDefault(field.Id)?.Revision ?? 0;
                    state.Answers[field.Id] = new(command.Value, "draft", "you", null, revision + 1);
                    if (state.Run?.Question?.FieldId == field.Id && !string.IsNullOrWhiteSpace(command.Value))
                    { state.Run.Question = null; state.Run.Status = "complete"; }
                    break;
                case "submit":
                    if (field is null || !state.Answers.TryGetValue(field.Id, out var answer) || !IntakeRules.Valid(field, answer.Value))
                        throw new IntakeException(422, "Add a valid answer before submitting.");
                    state.Answers[field.Id] = answer with { Status = "submitted" };
                    break;
                case "submitPage":
                    if (command.Page is not ("general" or "tasks")) throw new IntakeException(400, "Unknown page.");
                    var required = spec.Fields.Where(x => x.Page == command.Page).ToArray();
                    if (required.Any(x => !state.Answers.TryGetValue(x.Id, out var value) || !IntakeRules.Valid(x, value.Value)))
                        throw new IntakeException(422, "This page still has unanswered or invalid items.");
                    foreach (var item in required) state.Answers[item.Id] = state.Answers[item.Id] with { Status = "submitted" };
                    break;
                case "reply":
                    if (state.Run is not { Status: "waiting", Question: not null }) throw new IntakeException(409, "No clarification is waiting.");
                    state.ReplyReady = true;
                    break;
                case "question":
                    if (field is null || string.IsNullOrWhiteSpace(command.Value) || command.Value.Length > 1000)
                        throw new IntakeException(400, "Enter a question under 1000 characters.");
                    state.Questions[field.Id] = command.Value;
                    break;
                case "reset":
                    state.Answers.Clear(); state.Questions.Clear(); state.Run = null; state.ReplyReady = false;
                    break;
                default: throw new IntakeException(400, "Unknown request action.");
            }
            state.Receipts.Add(new(command.CommandId, fingerprint));
            if (state.Receipts.Count > 64) state.Receipts.RemoveAt(0);
        }, cancellation);
    internal static string Fingerprint(object command) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(command.GetType().Name + JsonSerializer.Serialize(command, command.GetType()))));
}
public sealed record IntakeSnapshot(long Version, IntakeState State);
