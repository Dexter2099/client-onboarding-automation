using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

[Trait("Acceptance", "AC-01 through AC-07, AC-09, AC-11")]
[Trait("Category", "Integration")]
public sealed partial class WorkflowTests : IClassFixture<FoundationFixture>
{
    private readonly FoundationFixture fixture;
    public WorkflowTests(FoundationFixture fixture)
    {
        this.fixture = fixture;
        fixture.Clock.Now = DateTimeOffset.UtcNow;
    }

    private sealed record Visitor(HttpClient Client, string Token, SessionView Session, ProjectView[] Projects);

    private async Task<Visitor> VisitorSession()
    {
        var client = fixture.Client();
        var protection = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf");
        var token = protection!["token"];
        var created = await Send(client, token, "/api/session", new { });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return new(client, token, (await created.Content.ReadFromJsonAsync<SessionView>())!,
            (await client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Post(Visitor visitor, string action, object body, int project = 0) =>
        Send(visitor.Client, visitor.Token, $"/api/projects/{visitor.Projects[project].Id}/{action}", body);

    private static async Task<WorkflowView> Success(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<WorkflowView>())!;
    }

    private async Task<WorkflowView> OriginalReply(Visitor visitor)
    {
        var started = await Success(await Post(visitor, "start", new { }));
        return await Success(await Post(visitor, "reply", new { version = started.Onboarding!.Version, sample = "initial" }));
    }
    private static ItemView Item(WorkflowView view, string kind) => view.Items.Single(x => x.Kind == kind);
    private static RevisionView Current(WorkflowView view, string kind) => Item(view, kind).Revisions.Last();
    private Task<HttpResponseMessage> Decision(Visitor visitor, WorkflowView view, string kind, string decision, string? comment = null) =>
        Post(visitor, "review", new { version = view.Onboarding!.Version, revisionId = Current(view, kind).Id, decision, comment });

    private async Task<WorkflowView> AllApproved(Visitor visitor)
    {
        var view = await OriginalReply(visitor);
        view = await Success(await Decision(visitor, view, "goals", "approved"));
        view = await Success(await Decision(visitor, view, "assets", "approved"));
        view = await Success(await Decision(visitor, view, "pages", "changes_requested", "Please remove Shop and confirm the five signed pages."));
        view = await Success(await Post(visitor, "reply", new { version = view.Onboarding!.Version,
            sample = "corrected", revisionId = Current(view, "pages").Id }));
        return await Success(await Decision(visitor, view, "pages", "approved"));
    }

    [Fact]
    public async Task Main_journey_preserves_revision_history_and_saves_an_approved_only_brief()
    {
        var visitor = await VisitorSession();
        var approved = await AllApproved(visitor);
        Assert.True(approved.CanMarkReady);
        Assert.Null(approved.Brief);
        Assert.Null(approved.Onboarding!.ReadyAt);
        Assert.Equal("Review complete", approved.State);
        var ready = await Success(await Post(visitor, "ready", new { version = approved.Onboarding.Version }));
        Assert.Equal("Ready for kickoff", ready.State);
        Assert.False(ready.CanMarkReady);
        Assert.NotNull(ready.Brief);
        Assert.Equal(visitor.Projects[0].SignedPages, ready.Brief.Pages);
        Assert.DoesNotContain("Shop", ready.Brief.Pages);
        Assert.Equal("Alex Morgan", ready.Brief.Owner);
        Assert.Equal(ready.Onboarding!.ReadyAt, ready.Onboarding.ReviewTaskClosedAt);
        Assert.Equal(ready.Onboarding.ReadyAt, ready.Brief.SavedAt);
        Assert.Equal(3, ready.Brief.Approvals.Length);
        Assert.Equal(Current(ready, "goals").Answer.Goal, ready.Brief.Goal);
        Assert.All(ready.Brief.Approvals, approval =>
        {
            Assert.Equal(Current(ready, approval.Kind).Id, approval.RevisionId);
            Assert.Equal("Alex Morgan", approval.ReviewedBy);
        });
        var pages = Item(ready, "pages").Revisions;
        Assert.Equal(2, pages.Length);
        Assert.Contains("Shop", pages[0].Answer.Pages);
        Assert.Equal("changes_requested", pages[0].Decision);
        Assert.NotNull(pages[0].Comment);
        Assert.Equal("approved", pages[1].Decision);
        Assert.Single(Item(ready, "goals").Revisions);
        Assert.Single(Item(ready, "assets").Revisions);
        Assert.Equal(2, ready.Outbox.Length);
        Assert.Contains(ready.Activity, x => x.Actor == "Maya Ellis (demo)" && x.Action == "Corrected Pages received");
        Assert.Contains(ready.Activity, x => x.Actor == "Alex Morgan" && x.Action == "Ready for kickoff");
        var loaded = await visitor.Client.GetFromJsonAsync<WorkflowView>($"/api/projects/{visitor.Projects[0].Id}/onboarding");
        Assert.Equal(JsonSerializer.Serialize(ready.Brief), JsonSerializer.Serialize(loaded!.Brief));
        Assert.Equal(visitor.Session, await visitor.Client.GetFromJsonAsync<SessionView>("/api/session"));
    }

    [Fact]
    public async Task Reply_is_not_approval_and_out_of_scope_Pages_cannot_be_approved()
    {
        var visitor = await VisitorSession();
        var view = await OriginalReply(visitor);
        Assert.All(view.Items, item => Assert.Equal("Awaiting review", item.State));
        Assert.All(view.Items, item => Assert.Null(item.Revisions[0].Decision));
        Assert.Contains("Shop", Current(view, "pages").Answer.Pages);
        Assert.NotNull(Item(view, "pages").Validation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Decision(visitor, view, "pages", "approved")).StatusCode);
        var saved = await visitor.Client.GetFromJsonAsync<WorkflowView>($"/api/projects/{visitor.Projects[0].Id}/onboarding");
        Assert.Null(Current(saved!, "pages").Decision);
        Assert.Null(saved!.Brief);
    }

    [Fact]
    public async Task Missing_correction_comment_cannot_create_a_decision_or_message()
    {
        var visitor = await VisitorSession();
        var view = await OriginalReply(visitor);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Decision(visitor, view, "pages", "changes_requested", "   ")).StatusCode);
        var saved = (await visitor.Client.GetFromJsonAsync<WorkflowView>($"/api/projects/{visitor.Projects[0].Id}/onboarding"))!;
        Assert.Single(saved.Outbox);
        Assert.Null(Current(saved, "pages").Decision);
        var correction = await Success(await Decision(visitor, saved, "pages", "changes_requested", "  Please remove Shop.  "));
        Assert.Equal("Please remove Shop.", Current(correction, "pages").Comment);
        Assert.Contains(correction.Outbox, x => x.Body == "Please remove Shop." && x.Recipient == "Maya Ellis");
    }

    [Fact]
    public async Task Corrected_Pages_needs_fresh_approval_and_rejects_old_revision_and_version()
    {
        var visitor = await VisitorSession();
        var view = await OriginalReply(visitor);
        view = await Success(await Decision(visitor, view, "goals", "approved"));
        view = await Success(await Decision(visitor, view, "assets", "approved"));
        var goalsId = Current(view, "goals").Id;
        var assetsId = Current(view, "assets").Id;
        view = await Success(await Decision(visitor, view, "pages", "changes_requested", "Remove Shop."));
        var oldId = Current(view, "pages").Id;
        var oldVersion = view.Onboarding!.Version;
        var corrected = await Success(await Post(visitor, "reply", new { version = oldVersion, sample = "corrected", revisionId = oldId }));
        Assert.Null(Current(corrected, "pages").Decision);
        Assert.False(corrected.CanMarkReady);
        Assert.Equal(goalsId, Current(corrected, "goals").Id);
        Assert.Equal(assetsId, Current(corrected, "assets").Id);
        Assert.Equal("approved", Current(corrected, "goals").Decision);
        Assert.Equal("approved", Current(corrected, "assets").Decision);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "review", new {
            version = corrected.Onboarding!.Version, revisionId = oldId, decision = "approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "review", new {
            version = oldVersion, revisionId = Current(corrected, "pages").Id, decision = "approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "ready", new { version = corrected.Onboarding.Version })).StatusCode);
    }

    [Fact]
    public async Task Readiness_requires_complete_current_approvals_then_freezes_replies_and_reviews()
    {
        var visitor = await VisitorSession();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "ready", new { version = 0 })).StatusCode);
        var approved = await AllApproved(visitor);
        var ready = await Success(await Post(visitor, "ready", new { version = approved.Onboarding!.Version }));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "reply", new { version = ready.Onboarding!.Version, sample = "initial" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decision(visitor, ready, "pages", "changes_requested", "Change it again.")).StatusCode);
        var same = await Success(await Post(visitor, "ready", new { version = approved.Onboarding!.Version }));
        Assert.Equal(JsonSerializer.Serialize(ready.Brief), JsonSerializer.Serialize(same.Brief));
        Assert.Equal(JsonSerializer.Serialize(ready.Onboarding), JsonSerializer.Serialize(same.Onboarding));
        Assert.Equal(ready.Activity.Length, same.Activity.Length);
    }

    [Fact]
    public async Task Every_workflow_endpoint_enforces_project_ownership()
    {
        var owner = await VisitorSession();
        var view = await OriginalReply(owner);
        var other = await VisitorSession();
        var path = $"/api/projects/{owner.Projects[0].Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"{path}/onboarding")).StatusCode);
        foreach (var (action, body) in new (string, object)[] {
            ("start", new { }), ("reply", new { version = view.Onboarding!.Version, sample = "initial" }),
            ("review", new { version = view.Onboarding!.Version, revisionId = Current(view, "goals").Id, decision = "approved" }),
            ("ready", new { version = view.Onboarding!.Version }),
            ("restore", new { version = view.Onboarding!.Version }),
            ("welcome", new { version = view.Onboarding!.Version, expectedAttempts = 1, commandId = Guid.NewGuid() }),
            ("publish", new { }) })
            Assert.Equal(HttpStatusCode.NotFound, (await Send(other.Client, other.Token, $"{path}/{action}", body)).StatusCode);
    }

    [Fact]
    public async Task Expiry_blocks_new_workflow_mutations_without_extending_the_sandbox()
    {
        var visitor = await VisitorSession();
        var view = await OriginalReply(visitor);
        fixture.Clock.Now = visitor.Session.ExpiresAt;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await visitor.Client.GetAsync($"/api/projects/{visitor.Projects[0].Id}/onboarding")).StatusCode);
        foreach (var (action, body) in new (string, object)[] {
            ("start", new { }), ("reply", new { version = view.Onboarding!.Version, sample = "initial" }),
            ("review", new { version = view.Onboarding!.Version, revisionId = Current(view, "goals").Id, decision = "approved" }),
            ("ready", new { version = view.Onboarding!.Version }),
            ("restore", new { version = view.Onboarding!.Version }),
            ("welcome", new { version = view.Onboarding!.Version, expectedAttempts = 1, commandId = Guid.NewGuid() }),
            ("publish", new { }) })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(visitor, action, body)).StatusCode);
    }

    [Fact]
    public async Task Reset_cascades_workflow_history_and_leaves_another_visitor_intact()
    {
        var visitor = await VisitorSession();
        var view = await AllApproved(visitor);
        view = await Success(await Post(visitor, "ready", new { version = view.Onboarding!.Version }));
        view = await Success(await Post(visitor, "publish", new { }));
        var other = await VisitorSession();
        var otherView = await OriginalReply(other);
        var reset = await Send(visitor.Client, visitor.Token, "/api/session/reset", new { });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        Assert.False(await db.Onboardings.AnyAsync(x => x.Id == view.Onboarding!.Id));
        Assert.False(await db.RequestItems.AnyAsync(x => x.OnboardingId == view.Onboarding!.Id));
        var itemIds = view.Items.Select(x => x.Id).ToArray();
        Assert.False(await db.ItemRevisions.AnyAsync(x => itemIds.Contains(x.RequestItemId)));
        Assert.False(await db.OutboxMessages.AnyAsync(x => x.OnboardingId == view.Onboarding!.Id));
        Assert.False(await db.ActivityEvents.AnyAsync(x => x.OnboardingId == view.Onboarding!.Id));
        Assert.False(await db.WelcomeAttempts.AnyAsync(x => x.OnboardingId == view.Onboarding!.Id));
        Assert.False(await db.RegisterEntries.AnyAsync(x => x.OnboardingId == view.Onboarding!.Id));
        var saved = await other.Client.GetFromJsonAsync<WorkflowView>($"/api/projects/{other.Projects[0].Id}/onboarding");
        Assert.Equal(JsonSerializer.Serialize(otherView.Onboarding), JsonSerializer.Serialize(saved!.Onboarding));
    }

    [Fact]
    public async Task Two_projects_for_the_same_client_have_distinct_onboardings_and_tasks()
    {
        var visitor = await VisitorSession();
        var first = await Success(await Post(visitor, "start", new { }, 0));
        var second = await Success(await Post(visitor, "start", new { }, 1));
        Assert.NotEqual(first.Onboarding!.Id, second.Onboarding!.Id);
        Assert.NotEqual(first.Onboarding.RequestId, second.Onboarding.RequestId);
        Assert.NotEqual(first.Onboarding.ReviewTaskId, second.Onboarding.ReviewTaskId);
        Assert.Single(first.Outbox);
        Assert.Empty(second.Outbox);
        Assert.False(Assert.Single(second.Onboarding.WelcomeAttempts).Prepared);
    }
}
