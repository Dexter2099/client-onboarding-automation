using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public static class WorkflowEndpoints
{
    private static readonly string[] Kinds = ["goals", "pages", "assets"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects/{id:guid}/onboarding", async (Guid id, HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await SandboxEndpoints.FindSession(http, db, false);
            if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
            var project = await OwnedProject(db, session!, id, http.RequestAborted);
            if (project is null) return Results.NotFound();
            var onboarding = await Load(db, id, http.RequestAborted);
            return SandboxEndpoints.Active(session, clock) ? Results.Ok(View(project, onboarding)) : SandboxEndpoints.Unavailable(http);
        });
        app.MapPost("/api/projects/{id:guid}/start", Start);
        app.MapPost("/api/projects/{id:guid}/reply", (Guid id, SampleReplyRequest request, HttpContext http,
            FoundationDb db, TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                Reply(project, onboarding, request, db, clock)));
        app.MapPost("/api/projects/{id:guid}/review", (Guid id, ReviewRequest request, HttpContext http,
            FoundationDb db, TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                Review(project, onboarding, request, db, clock)));
        app.MapPost("/api/projects/{id:guid}/ready", (Guid id, ReadyRequest request, HttpContext http,
            FoundationDb db, TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                MarkReady(project, onboarding, request, db, clock)));
        app.MapPost("/api/projects/{id:guid}/welcome", (Guid id, WelcomeRequest request, HttpContext http,
            FoundationDb db, TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                TryWelcome(project, onboarding, request, db, clock)));
        app.MapPost("/api/projects/{id:guid}/restore", (Guid id, ReadyRequest request, HttpContext http,
            FoundationDb db, TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                Restore(project, onboarding, request, db, clock)));
        app.MapPost("/api/projects/{id:guid}/publish", (Guid id, HttpContext http, FoundationDb db,
            TimeProvider clock) => Change(id, http, db, clock, (project, onboarding) =>
                Publish(project, onboarding, db, clock)));
    }

    private static async Task<IResult> Start(Guid id, HttpContext http, FoundationDb db, TimeProvider clock)
    {
        // Persist fixed setup before preparing the fictional welcome message.
        await using (var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted))
        {
            var session = await SandboxEndpoints.FindSession(http, db, true);
            if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
            var project = await OwnedProject(db, session!, id, http.RequestAborted);
            if (project is null) return Results.NotFound();
            var existing = await Load(db, id, http.RequestAborted);
            if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
            if (existing is not null) return Results.Ok(View(project, existing));

            var onboarding = new Onboarding
            {
                Id = Guid.NewGuid(), ProjectId = id, RequestId = Guid.NewGuid(), ReviewTaskId = Guid.NewGuid(),
                WelcomeServiceAvailable = project.Reference != "OF-1043",
                Items = Kinds.Select(kind => new RequestItem { Id = Guid.NewGuid(), Kind = kind }).ToList()
            };
            db.Onboardings.Add(onboarding);
            Event(db, onboarding, project.Owner, "Onboarding started", "Prepared",
                "Information request and staff review task prepared for this project.", clock);
            await db.SaveChangesAsync(http.RequestAborted);
            if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
            await transaction.CommitAsync(http.RequestAborted);
        }

        // A concurrent explicit welcome may have completed after setup committed.
        db.ChangeTracker.Clear();
        await using var welcomeTransaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
        var owner = await SandboxEndpoints.FindSession(http, db, true);
        if (!SandboxEndpoints.Active(owner, clock)) return SandboxEndpoints.Unavailable(http);
        var ownedProject = await OwnedProject(db, owner!, id, http.RequestAborted);
        if (ownedProject is null) return Results.NotFound();
        var setup = (await Load(db, id, http.RequestAborted))!;
        if (setup.WelcomeAttempts.Count == 0)
        {
            PrepareWelcome(ownedProject, setup, new(setup.Version, 0, Guid.Empty), db, clock);
            await db.SaveChangesAsync(http.RequestAborted);
        }
        if (!SandboxEndpoints.Active(owner, clock)) return SandboxEndpoints.Unavailable(http);
        await welcomeTransaction.CommitAsync(http.RequestAborted);
        return Results.Ok(View(ownedProject, setup));
    }

    private static IResult? TryWelcome(Project project, Onboarding onboarding, WelcomeRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (request.CommandId == Guid.Empty) return Problem(400, "A welcome retry needs a command ID.");
        var saved = onboarding.WelcomeAttempts.SingleOrDefault(x => x.CommandId == request.CommandId);
        if (saved is not null)
            return saved.ExpectedVersion == request.Version && saved.ExpectedAttempts == request.ExpectedAttempts
                ? Results.Ok(View(project, onboarding)) : Problem(409, "This command ID was already used with different inputs.");
        if (onboarding.WelcomePreparedAt is not null) return Results.Ok(View(project, onboarding));
        if (onboarding.WelcomeAttempts.Count >= 3)
            return Problem(409, "All three welcome attempts are used. Completed work is saved. Reset both sample projects for a fresh sandbox.");
        if (request.Version != onboarding.Version || request.ExpectedAttempts != onboarding.WelcomeAttempts.Count)
            return Stale();
        PrepareWelcome(project, onboarding, request, db, clock);
        return null;
    }

    private static void PrepareWelcome(Project project, Onboarding onboarding, WelcomeRequest request,
        FoundationDb db, TimeProvider clock)
    {
        var attempt = new WelcomeAttempt { Id = Guid.NewGuid(), OnboardingId = onboarding.Id,
            Number = onboarding.WelcomeAttempts.Count + 1, CommandId = request.CommandId,
            ExpectedVersion = request.Version, ExpectedAttempts = request.ExpectedAttempts,
            Prepared = onboarding.WelcomeServiceAvailable, At = Now(clock) };
        onboarding.WelcomeAttempts.Add(attempt);
        db.WelcomeAttempts.Add(attempt);
        if (attempt.Prepared)
        {
            Message(db, onboarding, project.ClientContact, "welcome", $"{project.Reference}: welcome to your website project",
                $"Hi {project.ClientContact}, please reply with your goals and audience, confirm the five agreed pages, " +
                "and share the sample brand-asset references. Alex will review your answers before kickoff.", clock);
            onboarding.WelcomePreparedAt = attempt.At;
        }
        onboarding.Version++;
        Event(db, onboarding, "OnboardFlow (simulation)",
            attempt.Prepared ? "Welcome message prepared" : "Welcome preparation failed",
            attempt.Prepared ? "Simulated" : attempt.Number == 3 ? "Exhausted" : "Failed",
            attempt.Prepared ? $"Attempt {attempt.Number} of 3 saved in the fictional Outbox. No email was sent."
                : $"Attempt {attempt.Number} of 3: simulated welcome service unavailable. Request and review task remain saved.", clock);
    }

    private static IResult? Restore(Project project, Onboarding onboarding, ReadyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.WelcomeServiceAvailable) return Results.Ok(View(project, onboarding));
        if (request.Version != onboarding.Version) return Stale();
        onboarding.WelcomeServiceAvailable = true;
        onboarding.Version++;
        Event(db, onboarding, project.Owner, "Simulated service restored", "Available",
            "Demo control restored welcome availability. Attempts used are unchanged; no message was retried.", clock);
        return null;
    }

    private static IResult? Publish(Project project, Onboarding onboarding, FoundationDb db, TimeProvider clock)
    {
        if (onboarding.RegisterEntry is not null) return Results.Ok(View(project, onboarding));
        if (onboarding.ReadyAt is null || onboarding.BriefJson is null)
            return Problem(409, "Mark the project ready before publishing its simulated handoff.");
        var id = Guid.NewGuid();
        var entry = new RegisterEntry { Id = id, OnboardingId = onboarding.Id, Reference = $"SIM-{id:N}",
            BriefJson = onboarding.BriefJson, ConfirmedAt = Now(clock) };
        onboarding.RegisterEntry = entry;
        db.RegisterEntries.Add(entry);
        onboarding.Version++;
        Event(db, onboarding, project.Owner, "Simulated handoff confirmed", "Simulated",
            $"Saved register entry {entry.Reference}. No Make or Google Sheets connection was used.", clock);
        return null;
    }

    // All project mutations take the same session lock as Reset; no repository or job framework.
    private static async Task<IResult> Change(Guid id, HttpContext http, FoundationDb db, TimeProvider clock,
        Func<Project, Onboarding, IResult?> apply)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
        var session = await SandboxEndpoints.FindSession(http, db, true);
        if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
        var project = await OwnedProject(db, session!, id, http.RequestAborted);
        if (project is null) return Results.NotFound();
        var onboarding = await Load(db, id, http.RequestAborted);
        if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
        if (onboarding is null) return Problem(409, "Start onboarding before reviewing client replies.");
        var result = apply(project, onboarding);
        if (result is not null) return result;
        await db.SaveChangesAsync(http.RequestAborted);
        if (!SandboxEndpoints.Active(session, clock)) return SandboxEndpoints.Unavailable(http);
        await transaction.CommitAsync(http.RequestAborted);
        return Results.Ok(View(project, onboarding));
    }

    private static IResult? Reply(Project project, Onboarding onboarding, SampleReplyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return Frozen();
        if (onboarding.WelcomePreparedAt is null) return Problem(409, "Complete setup before loading a reply.");
        if (request.Sample == "initial")
        {
            if (onboarding.Items.All(x => x.Revisions.Any(r => r.SubmissionKey == "initial")))
                return Results.Ok(View(project, onboarding));
            if (request.Version != onboarding.Version) return Stale();
            foreach (var item in onboarding.Items)
                Revision(db, item, "initial", Sample(item.Kind, project, false), project.ClientContact, clock);
        }
        else if (request.Sample == "corrected")
        {
            var pages = onboarding.Items.Single(x => x.Kind == "pages");
            var key = $"correction:{request.RevisionId}";
            if (pages.Revisions.Any(x => x.SubmissionKey == key)) return Results.Ok(View(project, onboarding));
            if (request.Version != onboarding.Version) return Stale();
            var latest = Latest(pages);
            if (latest?.Id != request.RevisionId || latest?.Decision != "changes_requested")
                return Problem(409, "Request a correction on the current Pages reply first.");
            Revision(db, pages, key, Sample("pages", project, true), project.ClientContact, clock);
        }
        else return Problem(400, "Choose the initial or corrected sample reply.");

        onboarding.Version++;
        Event(db, onboarding, $"{project.ClientContact} (demo)",
            request.Sample == "initial" ? "Sample client reply received" : "Corrected Pages received",
            "Awaiting review", "Demo control supplied fictional client information. Nothing was approved automatically.", clock);
        return null;
    }

    private static IResult? Review(Project project, Onboarding onboarding, ReviewRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return Frozen();
        var item = onboarding.Items.SingleOrDefault(x => x.Revisions.Any(r => r.Id == request.RevisionId));
        if (item is null) return Results.NotFound();
        var revision = Latest(item)!;
        if (revision.Id != request.RevisionId) return Stale();
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (request.Decision is not ("approved" or "changes_requested")) return Problem(400, "Choose a review decision.");
        if (request.Decision == "changes_requested" && comment is null) return Problem(400, "Add a correction comment before requesting changes.");
        if (comment?.Length > 1000) return Problem(400, "Keep the correction comment within 1,000 characters.");
        if (request.Decision == "approved" && comment is not null) return Problem(400, "Use Request changes to record a correction comment.");
        if (request.Decision == "changes_requested" && item.Kind != "pages")
            return Problem(400, "This prepared example supplies corrections for Pages only.");
        if (revision.Decision is not null)
            return revision.Decision == request.Decision && revision.Comment == comment
                ? Results.Ok(View(project, onboarding)) : Stale();
        if (request.Version != onboarding.Version) return Stale();
        var validation = Validate(item.Kind, Answer(revision), project);
        if (request.Decision == "approved" && validation is not null) return Problem(422, validation);

        revision.Decision = request.Decision;
        revision.Comment = comment;
        revision.ReviewedBy = project.Owner;
        revision.ReviewedAt = Now(clock);
        if (request.Decision == "changes_requested")
            Message(db, onboarding, project.ClientContact, $"correction:{revision.Id}",
                $"{project.Reference}: please correct Pages", comment!, clock);
        onboarding.Version++;
        Event(db, onboarding, project.Owner,
            $"{Label(item.Kind)}: {(request.Decision == "approved" ? "approved" : "changes requested")}",
            request.Decision == "approved" ? "Approved" : "Changes requested",
            comment ?? $"Approved revision {revision.Number}.", clock);
        return null;
    }

    private static IResult? MarkReady(Project project, Onboarding onboarding, ReadyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return Results.Ok(View(project, onboarding));
        if (request.Version != onboarding.Version) return Stale();
        if (!Eligible(project, onboarding)) return Problem(409, "Approve every current reply before marking the project ready.");
        var goals = Answer(Latest(onboarding.Items.Single(x => x.Kind == "goals"))!);
        var pages = Answer(Latest(onboarding.Items.Single(x => x.Kind == "pages"))!);
        var assets = Answer(Latest(onboarding.Items.Single(x => x.Kind == "assets"))!);
        var now = Now(clock);
        var approvals = Kinds.Select(kind =>
        {
            var revision = Latest(onboarding.Items.Single(x => x.Kind == kind))!;
            return new ApprovedRevision(kind, revision.Id, revision.Number, revision.ReviewedBy!, revision.ReviewedAt!.Value);
        }).ToArray();
        var brief = new KickoffBrief(project.Reference, project.Name, project.ClientName, project.ClientContact,
            project.Owner, project.Service, goals.Goal!, goals.Audience!, pages.Pages, assets.AssetReferences, approvals, now);
        onboarding.ReadyAt = now;
        onboarding.ReviewTaskClosedAt = now;
        onboarding.BriefJson = JsonSerializer.Serialize(brief, Json);
        onboarding.Version++;
        Event(db, onboarding, project.Owner, "Ready for kickoff", "Ready",
            "Approved brief saved; staff review task closed. Replies and review decisions are now read-only.", clock);
        return null;
    }

    private static Task<Project?> OwnedProject(FoundationDb db, SandboxSession session, Guid id, CancellationToken token) =>
        db.Projects.SingleOrDefaultAsync(x => x.Id == id && x.SandboxSessionId == session.Id, token);

    private static Task<Onboarding?> Load(FoundationDb db, Guid projectId, CancellationToken token) => db.Onboardings
        .Include(x => x.Items).ThenInclude(x => x.Revisions).Include(x => x.Messages).Include(x => x.Events)
        .Include(x => x.WelcomeAttempts).Include(x => x.RegisterEntry)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.ProjectId == projectId, token);

    private static WorkflowView View(Project project, Onboarding? onboarding)
    {
        if (onboarding is null) return new(project.Id, "Not started", false, null, [], [], [], null, null);
        var items = Kinds.Select(kind =>
        {
            var item = onboarding.Items.Single(x => x.Kind == kind);
            var latest = Latest(item);
            var state = latest is null ? "Awaiting reply" : latest.Decision switch
            {
                "approved" => "Approved", "changes_requested" => "Changes requested", _ => "Awaiting review"
            };
            return new ItemView(item.Id, kind, Label(kind), state,
                latest is null ? null : Validate(kind, Answer(latest), project),
                item.Revisions.OrderBy(x => x.Number).Select(x => new RevisionView(x.Id, x.Number, Answer(x),
                    x.SubmittedBy, x.SubmittedAt, x.Decision, x.Comment, x.ReviewedBy, x.ReviewedAt)).ToArray());
        }).ToArray();
        var eligible = Eligible(project, onboarding);
        var state = onboarding.ReadyAt is not null ? "Ready for kickoff"
            : onboarding.WelcomePreparedAt is null ? "Setup incomplete"
            : items.All(x => x.Revisions.Length == 0) ? "Waiting for client"
            : items.Any(x => x.State == "Changes requested") ? "Waiting for correction"
            : eligible ? "Review complete" : "Needs review";
        return new(project.Id, state, eligible && onboarding.ReadyAt is null,
            new OnboardingView(onboarding.Id, onboarding.RequestId, onboarding.ReviewTaskId, onboarding.Version,
                onboarding.WelcomePreparedAt, onboarding.ReadyAt, onboarding.ReviewTaskClosedAt,
                onboarding.WelcomeServiceAvailable, onboarding.WelcomeAttempts.OrderBy(x => x.Number)
                    .Select(x => new WelcomeAttemptView(x.Id, x.Number, x.CommandId, x.Prepared, x.At)).ToArray()), items,
            onboarding.Messages.OrderBy(x => x.PreparedAt).ThenBy(x => x.MessageKey)
                .Select(x => new MessageView(x.Id, x.Recipient, x.Subject, x.Body, x.PreparedAt)).ToArray(),
            onboarding.Events.OrderBy(x => x.Sequence).Select(x => new ActivityView(x.Id, x.Sequence, x.Actor,
                x.Action, x.Outcome, x.Details, x.At)).ToArray(),
            onboarding.BriefJson is null ? null : JsonSerializer.Deserialize<KickoffBrief>(onboarding.BriefJson, Json),
            onboarding.RegisterEntry is not { } entry ? null : new RegisterView(entry.Id, entry.Reference,
                entry.ConfirmedAt, JsonSerializer.Deserialize<KickoffBrief>(entry.BriefJson, Json)!));
    }

    private static bool Eligible(Project project, Onboarding onboarding) => onboarding.WelcomePreparedAt is not null &&
        onboarding.Items.Count == Kinds.Length && onboarding.Items.All(item => Latest(item) is { Decision: "approved" } revision &&
            Validate(item.Kind, Answer(revision), project) is null);
    private static ItemRevision? Latest(RequestItem item) => item.Revisions.MaxBy(x => x.Number);
    private static AnswerContent Answer(ItemRevision revision) => JsonSerializer.Deserialize<AnswerContent>(revision.AnswerJson, Json)!;
    private static string? Validate(string kind, AnswerContent answer, Project project) => kind switch
    {
        "pages" when answer.Pages.Length != project.SignedPages.Length ||
            !new HashSet<string>(answer.Pages, StringComparer.Ordinal).SetEquals(project.SignedPages) =>
            "Shop is outside the signed five-page scope. Request a correction before approving Pages.",
        "goals" when string.IsNullOrWhiteSpace(answer.Goal) || string.IsNullOrWhiteSpace(answer.Audience) => "Goals and audience are required.",
        "assets" when answer.AssetReferences.Length == 0 => "At least one sample asset reference is required.",
        _ => null
    };
    private static AnswerContent Sample(string kind, Project project, bool corrected) => kind switch
    {
        "goals" => new("Help small creative businesses discover Juniper's services and request a consultation.",
            "Small business owners planning a brand or website project.", [], []),
        "pages" => new(null, null, corrected ? project.SignedPages.ToArray() : [.. project.SignedPages, "Shop"], []),
        _ => new(null, null, [], ["Juniper logo (sample reference)", "Brand guide v1.pdf (sample reference)",
            "Homepage photography selection (sample reference)"])
    };
    private static string Label(string kind) => kind switch { "goals" => "Goals & audience", "pages" => "Pages", _ => "Brand assets" };
    private static DateTimeOffset Now(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }
    private static IResult Problem(int status, string title) => Results.Problem(statusCode: status, title: title);
    private static IResult Stale() => Problem(409, "This reply or project changed. Load the saved state before reviewing again.");
    private static IResult Frozen() => Problem(409, "This project is ready for kickoff. Replies and review decisions are read-only.");

    private static void Revision(FoundationDb db, RequestItem item, string key, AnswerContent answer, string actor, TimeProvider clock)
    {
        var revision = new ItemRevision { Id = Guid.NewGuid(), RequestItemId = item.Id, Number = item.Revisions.Count + 1,
            SubmissionKey = key, AnswerJson = JsonSerializer.Serialize(answer, Json), SubmittedBy = actor, SubmittedAt = Now(clock) };
        item.Revisions.Add(revision);
        db.ItemRevisions.Add(revision);
    }
    private static void Message(FoundationDb db, Onboarding onboarding, string recipient, string key, string subject, string body, TimeProvider clock)
    {
        var message = new OutboxMessage { Id = Guid.NewGuid(), OnboardingId = onboarding.Id, Recipient = recipient,
            MessageKey = key, Subject = subject, Body = body, PreparedAt = Now(clock) };
        onboarding.Messages.Add(message);
        db.OutboxMessages.Add(message);
    }
    private static void Event(FoundationDb db, Onboarding onboarding, string actor, string action, string outcome, string details, TimeProvider clock)
    {
        var activity = new ActivityEvent { Id = Guid.NewGuid(), OnboardingId = onboarding.Id,
            Sequence = onboarding.Events.Count + 1, Actor = actor, Action = action, Outcome = outcome, Details = details, At = Now(clock) };
        onboarding.Events.Add(activity);
        db.ActivityEvents.Add(activity);
    }
}
