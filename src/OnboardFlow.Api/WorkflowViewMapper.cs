using System.Text.Json;
using static OnboardFlow.Api.WorkflowRules;

namespace OnboardFlow.Api;

internal static class WorkflowViewMapper
{
    internal static WorkflowView Map(Project project, Onboarding? onboarding)
    {
        if (onboarding is null) return new(project.Id, "Not started", false, null, [], [], [], null, null);
        var items = Kinds.Select(kind =>
        {
            var item = onboarding.Items.Single(x => x.Kind == kind);
            var latest = Latest(item);
            var state = latest is null ? "Awaiting reply" : latest.Decision switch
            {
                "approved" => "Approved",
                "changes_requested" => "Changes requested",
                _ => "Awaiting review"
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

}
