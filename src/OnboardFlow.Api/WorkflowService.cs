using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static OnboardFlow.Api.WorkflowRules;

namespace OnboardFlow.Api;

public sealed class WorkflowService(FoundationDb db, TimeProvider clock)
{
    public async Task<WorkflowResult> Read(Guid id, string? token, CancellationToken cancellationToken)
    {
        var session = await SandboxSessionAccess.Find(token, db, false, cancellationToken);
        if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
        var project = await OwnedProject(db, session!, id, cancellationToken);
        if (project is null) return WorkflowResult.NotFound();
        var onboarding = await Load(db, id, cancellationToken);
        return SandboxSessionAccess.Active(session, clock)
            ? WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding)) : WorkflowResult.Unavailable();
    }

    public Task<WorkflowResult> Reply(Guid id, string? token, SampleReplyRequest request, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => Reply(project, onboarding, request, db, clock));
    public Task<WorkflowResult> Review(Guid id, string? token, ReviewRequest request, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => Review(project, onboarding, request, db, clock));
    public Task<WorkflowResult> MarkReady(Guid id, string? token, ReadyRequest request, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => MarkReady(project, onboarding, request, db, clock));
    public Task<WorkflowResult> TryWelcome(Guid id, string? token, WelcomeRequest request, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => TryWelcome(project, onboarding, request, db, clock));
    public Task<WorkflowResult> Restore(Guid id, string? token, ReadyRequest request, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => Restore(project, onboarding, request, db, clock));
    public Task<WorkflowResult> Publish(Guid id, string? token, CancellationToken cancellationToken)
        => Change(id, token, cancellationToken, (project, onboarding) => Publish(project, onboarding, db, clock));

    public async Task<WorkflowResult> Start(Guid id, string? token, CancellationToken cancellationToken)
    {
        // Persist fixed setup before preparing the fictional welcome message.
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var session = await SandboxSessionAccess.Find(token, db, true, cancellationToken);
            if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
            var project = await OwnedProject(db, session!, id, cancellationToken);
            if (project is null) return WorkflowResult.NotFound();
            var existing = await Load(db, id, cancellationToken);
            if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
            if (existing is not null) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, existing));

            var onboarding = new Onboarding
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                RequestId = Guid.NewGuid(),
                ReviewTaskId = Guid.NewGuid(),
                WelcomeServiceAvailable = project.Reference != "OF-1043",
                Items = Kinds.Select(kind => new RequestItem { Id = Guid.NewGuid(), Kind = kind }).ToList()
            };
            db.Onboardings.Add(onboarding);
            Event(db, onboarding, project.Owner, "Onboarding started", "Prepared",
                "Information request and staff review task prepared for this project.", clock);
            await db.SaveChangesAsync(cancellationToken);
            if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
            await transaction.CommitAsync(cancellationToken);
        }

        // A concurrent explicit welcome may have completed after setup committed.
        db.ChangeTracker.Clear();
        await using var welcomeTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var owner = await SandboxSessionAccess.Find(token, db, true, cancellationToken);
        if (!SandboxSessionAccess.Active(owner, clock)) return WorkflowResult.Unavailable();
        var ownedProject = await OwnedProject(db, owner!, id, cancellationToken);
        if (ownedProject is null) return WorkflowResult.NotFound();
        var setup = (await Load(db, id, cancellationToken))!;
        if (setup.WelcomeAttempts.Count == 0)
        {
            PrepareWelcome(ownedProject, setup, new(setup.Version, 0, Guid.Empty), db, clock);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (!SandboxSessionAccess.Active(owner, clock)) return WorkflowResult.Unavailable();
        await welcomeTransaction.CommitAsync(cancellationToken);
        return WorkflowResult.Ok(WorkflowViewMapper.Map(ownedProject, setup));
    }

    private static WorkflowResult? TryWelcome(Project project, Onboarding onboarding, WelcomeRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (request.CommandId == Guid.Empty) return WorkflowResult.Invalid("A welcome retry needs a command ID.");
        var saved = onboarding.WelcomeAttempts.SingleOrDefault(x => x.CommandId == request.CommandId);
        if (saved is not null)
            return saved.ExpectedVersion == request.Version && saved.ExpectedAttempts == request.ExpectedAttempts
                ? WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding)) : WorkflowResult.Conflict("This command ID was already used with different inputs.");
        if (onboarding.WelcomePreparedAt is not null) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
        if (onboarding.WelcomeAttempts.Count >= 3)
            return WorkflowResult.Conflict("All three welcome attempts are used. Completed work is saved. Reset both sample projects for a fresh sandbox.");
        if (request.Version != onboarding.Version || request.ExpectedAttempts != onboarding.WelcomeAttempts.Count)
            return Stale();
        PrepareWelcome(project, onboarding, request, db, clock);
        return null;
    }

    private static void PrepareWelcome(Project project, Onboarding onboarding, WelcomeRequest request,
        FoundationDb db, TimeProvider clock)
    {
        var attempt = new WelcomeAttempt
        {
            Id = Guid.NewGuid(),
            OnboardingId = onboarding.Id,
            Number = onboarding.WelcomeAttempts.Count + 1,
            CommandId = request.CommandId,
            ExpectedVersion = request.Version,
            ExpectedAttempts = request.ExpectedAttempts,
            Prepared = onboarding.WelcomeServiceAvailable,
            At = Now(clock)
        };
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

    private static WorkflowResult? Restore(Project project, Onboarding onboarding, ReadyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.WelcomeServiceAvailable) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
        if (request.Version != onboarding.Version) return Stale();
        onboarding.WelcomeServiceAvailable = true;
        onboarding.Version++;
        Event(db, onboarding, project.Owner, "Simulated service restored", "Available",
            "Demo control restored welcome availability. Attempts used are unchanged; no message was retried.", clock);
        return null;
    }

    private static WorkflowResult? Publish(Project project, Onboarding onboarding, FoundationDb db, TimeProvider clock)
    {
        if (onboarding.RegisterEntry is not null) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
        if (onboarding.ReadyAt is null || onboarding.BriefJson is null)
            return WorkflowResult.Conflict("Mark the project ready before publishing its simulated handoff.");
        var id = Guid.NewGuid();
        var entry = new RegisterEntry
        {
            Id = id,
            OnboardingId = onboarding.Id,
            Reference = $"SIM-{id:N}",
            BriefJson = onboarding.BriefJson,
            ConfirmedAt = Now(clock)
        };
        onboarding.RegisterEntry = entry;
        db.RegisterEntries.Add(entry);
        onboarding.Version++;
        Event(db, onboarding, project.Owner, "Simulated handoff confirmed", "Simulated",
            $"Saved register entry {entry.Reference}. No Make or Google Sheets connection was used.", clock);
        return null;
    }

    // All project mutations take the same session lock as Reset; no repository or job framework.
    private async Task<WorkflowResult> Change(Guid id, string? token, CancellationToken cancellationToken,
        Func<Project, Onboarding, WorkflowResult?> apply)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var session = await SandboxSessionAccess.Find(token, db, true, cancellationToken);
        if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
        var project = await OwnedProject(db, session!, id, cancellationToken);
        if (project is null) return WorkflowResult.NotFound();
        var onboarding = await Load(db, id, cancellationToken);
        if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
        if (onboarding is null) return WorkflowResult.Conflict("Start onboarding before reviewing client replies.");
        var result = apply(project, onboarding);
        if (result is not null) return result;
        await db.SaveChangesAsync(cancellationToken);
        if (!SandboxSessionAccess.Active(session, clock)) return WorkflowResult.Unavailable();
        await transaction.CommitAsync(cancellationToken);
        return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
    }

    private static WorkflowResult? Reply(Project project, Onboarding onboarding, SampleReplyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return Frozen();
        if (onboarding.WelcomePreparedAt is null) return WorkflowResult.Conflict("Complete setup before loading a reply.");
        if (request.Sample == "initial")
        {
            if (onboarding.Items.All(x => x.Revisions.Any(r => r.SubmissionKey == "initial")))
                return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
            if (request.Version != onboarding.Version) return Stale();
            foreach (var item in onboarding.Items)
                Revision(db, item, "initial", Sample(item.Kind, project, false), project.ClientContact, clock);
        }
        else if (request.Sample == "corrected")
        {
            var pages = onboarding.Items.Single(x => x.Kind == "pages");
            var key = $"correction:{request.RevisionId}";
            if (pages.Revisions.Any(x => x.SubmissionKey == key)) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
            if (request.Version != onboarding.Version) return Stale();
            var latest = Latest(pages);
            if (latest?.Id != request.RevisionId || latest?.Decision != "changes_requested")
                return WorkflowResult.Conflict("Request a correction on the current Pages reply first.");
            Revision(db, pages, key, Sample("pages", project, true), project.ClientContact, clock);
        }
        else return WorkflowResult.Invalid("Choose the initial or corrected sample reply.");

        onboarding.Version++;
        Event(db, onboarding, $"{project.ClientContact} (demo)",
            request.Sample == "initial" ? "Sample client reply received" : "Corrected Pages received",
            "Awaiting review", "Demo control supplied fictional client information. Nothing was approved automatically.", clock);
        return null;
    }

    private static WorkflowResult? Review(Project project, Onboarding onboarding, ReviewRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return Frozen();
        var item = onboarding.Items.SingleOrDefault(x => x.Revisions.Any(r => r.Id == request.RevisionId));
        if (item is null) return WorkflowResult.NotFound();
        var revision = Latest(item)!;
        if (revision.Id != request.RevisionId) return Stale();
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (request.Decision is not ("approved" or "changes_requested")) return WorkflowResult.Invalid("Choose a review decision.");
        if (request.Decision == "changes_requested" && comment is null) return WorkflowResult.Invalid("Add a correction comment before requesting changes.");
        if (comment?.Length > 1000) return WorkflowResult.Invalid("Keep the correction comment within 1,000 characters.");
        if (request.Decision == "approved" && comment is not null) return WorkflowResult.Invalid("Use Request changes to record a correction comment.");
        if (request.Decision == "changes_requested" && item.Kind != "pages")
            return WorkflowResult.Invalid("This prepared example supplies corrections for Pages only.");
        if (revision.Decision is not null)
            return revision.Decision == request.Decision && revision.Comment == comment
                ? WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding)) : Stale();
        if (request.Version != onboarding.Version) return Stale();
        var validation = Validate(item.Kind, Answer(revision), project);
        if (request.Decision == "approved" && validation is not null) return WorkflowResult.OutOfScope(validation);

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

    private static WorkflowResult? MarkReady(Project project, Onboarding onboarding, ReadyRequest request,
        FoundationDb db, TimeProvider clock)
    {
        if (onboarding.ReadyAt is not null) return WorkflowResult.Ok(WorkflowViewMapper.Map(project, onboarding));
        if (request.Version != onboarding.Version) return Stale();
        if (!Eligible(project, onboarding)) return WorkflowResult.Conflict("Approve every current reply before marking the project ready.");
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

    private static WorkflowResult Stale() => WorkflowResult.Conflict("This reply or project changed. Load the saved state before reviewing again.");
    private static WorkflowResult Frozen() => WorkflowResult.Conflict("This project is ready for kickoff. Replies and review decisions are read-only.");

    private static void Revision(FoundationDb db, RequestItem item, string key, AnswerContent answer, string actor, TimeProvider clock)
    {
        var revision = new ItemRevision
        {
            Id = Guid.NewGuid(),
            RequestItemId = item.Id,
            Number = item.Revisions.Count + 1,
            SubmissionKey = key,
            AnswerJson = JsonSerializer.Serialize(answer, Json),
            SubmittedBy = actor,
            SubmittedAt = Now(clock)
        };
        item.Revisions.Add(revision);
        db.ItemRevisions.Add(revision);
    }
    private static void Message(FoundationDb db, Onboarding onboarding, string recipient, string key, string subject, string body, TimeProvider clock)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OnboardingId = onboarding.Id,
            Recipient = recipient,
            MessageKey = key,
            Subject = subject,
            Body = body,
            PreparedAt = Now(clock)
        };
        onboarding.Messages.Add(message);
        db.OutboxMessages.Add(message);
    }
    private static void Event(FoundationDb db, Onboarding onboarding, string actor, string action, string outcome, string details, TimeProvider clock)
    {
        var activity = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            OnboardingId = onboarding.Id,
            Sequence = onboarding.Events.Count + 1,
            Actor = actor,
            Action = action,
            Outcome = outcome,
            Details = details,
            At = Now(clock)
        };
        onboarding.Events.Add(activity);
        db.ActivityEvents.Add(activity);
    }
}
