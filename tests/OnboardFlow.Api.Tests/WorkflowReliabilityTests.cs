using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed partial class WorkflowTests
{
    private Task<WorkflowView?> Saved(Visitor visitor, int project = 0) =>
        visitor.Client.GetFromJsonAsync<WorkflowView>($"/api/projects/{visitor.Projects[project].Id}/onboarding");

    private static WelcomeRequest Retry(WorkflowView view) =>
        new(view.Onboarding!.Version, view.Onboarding.WelcomeAttempts.Length, Guid.NewGuid());

    [Fact]
    public async Task Repeated_and_overlapping_Start_reuse_all_outputs_after_a_lost_response()
    {
        var visitor = await VisitorSession();
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(visitor, "start", new { })));
        var views = await Task.WhenAll(responses.Select(Success));
        var saved = (await Saved(visitor))!;
        Assert.Single(saved.Outbox);
        Assert.Single(saved.Onboarding!.WelcomeAttempts);
        Assert.Equal(2, saved.Activity.Length);
        Assert.All(views, view =>
        {
            Assert.Equal(saved.Onboarding.Id, view.Onboarding!.Id);
            Assert.Equal(saved.Onboarding.RequestId, view.Onboarding.RequestId);
            Assert.Equal(saved.Onboarding.ReviewTaskId, view.Onboarding.ReviewTaskId);
            Assert.Equal(saved.Items.Select(x => x.Id), view.Items.Select(x => x.Id));
        });
        // The caller has discarded the successful responses: reload and replay still find the same output.
        var replay = await Success(await Post(visitor, "start", new { }));
        Assert.Equal(saved.Outbox[0].Id, replay.Outbox[0].Id);
        Assert.Equal(saved.Activity, replay.Activity);
        Assert.Equal(saved.Onboarding.WelcomeAttempts, replay.Onboarding!.WelcomeAttempts);
    }

    [Fact]
    public async Task Failure_preserves_setup_and_restore_then_attempt_two_recovers_without_resetting_budget()
    {
        var visitor = await VisitorSession();
        var failed = await Success(await Post(visitor, "start", new { }, 1));
        Assert.Equal("Setup incomplete", failed.State);
        Assert.Equal(3, failed.Items.Length);
        Assert.Empty(failed.Outbox);
        Assert.Null(failed.Onboarding!.WelcomePreparedAt);
        Assert.False(failed.Onboarding.WelcomeServiceAvailable);
        Assert.False(Assert.Single(failed.Onboarding.WelcomeAttempts).Prepared);
        var repeated = await Success(await Post(visitor, "start", new { }, 1));
        Assert.Equal(failed.Onboarding.WelcomeAttempts, repeated.Onboarding!.WelcomeAttempts);
        Assert.Equal(failed.Activity, repeated.Activity);
        var restored = await Success(await Post(visitor, "restore", new { version = failed.Onboarding.Version }, 1));
        Assert.Single(restored.Onboarding!.WelcomeAttempts);
        Assert.Empty(restored.Outbox);
        var again = await Success(await Post(visitor, "restore", new { version = failed.Onboarding.Version }, 1));
        Assert.Equal(restored.Activity, again.Activity);
        var command = Retry(restored);
        var recovered = await Success(await Post(visitor, "welcome", command, 1));
        Assert.Equal(failed.Onboarding.Id, recovered.Onboarding!.Id);
        Assert.Equal(failed.Onboarding.RequestId, recovered.Onboarding.RequestId);
        Assert.Equal(failed.Onboarding.ReviewTaskId, recovered.Onboarding.ReviewTaskId);
        Assert.Equal(new[] { false, true }, recovered.Onboarding.WelcomeAttempts.Select(x => x.Prepared));
        Assert.Single(recovered.Outbox);
        var replay = await Success(await Post(visitor, "welcome", command, 1));
        Assert.Equal(recovered.Activity, replay.Activity);
        Assert.Equal(recovered.Onboarding.WelcomeAttempts, replay.Onboarding!.WelcomeAttempts);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "welcome", command with { ExpectedAttempts = 0 }, 1)).StatusCode);
        var alreadyPrepared = await Success(await Post(visitor, "welcome", Retry(failed), 1));
        Assert.Equal(recovered.Outbox[0].Id, alreadyPrepared.Outbox[0].Id);
        Assert.Equal(2, alreadyPrepared.Onboarding!.WelcomeAttempts.Length);
        var first = await Success(await Post(visitor, "start", new { }));
        Assert.True(Assert.Single(first.Onboarding!.WelcomeAttempts).Prepared);
    }

    [Fact]
    public async Task Same_retry_command_can_overlap_but_new_commands_based_on_one_count_cannot_spend_twice()
    {
        var visitor = await VisitorSession();
        var failed = await Success(await Post(visitor, "start", new { }, 1));
        var command = Retry(failed);
        var same = await Task.WhenAll(Post(visitor, "welcome", command, 1), Post(visitor, "welcome", command, 1));
        var replays = await Task.WhenAll(same.Select(Success));
        Assert.All(replays, view => Assert.Equal(2, view.Onboarding!.WelcomeAttempts.Length));
        var latest = (await Saved(visitor, 1))!;
        var responses = await Task.WhenAll(Post(visitor, "welcome", Retry(latest), 1), Post(visitor, "welcome", Retry(latest), 1));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var exhausted = (await Saved(visitor, 1))!;
        Assert.Equal(3, exhausted.Onboarding!.WelcomeAttempts.Length);
        Assert.Empty(exhausted.Outbox);
    }

    [Fact]
    public async Task Three_failures_exhaust_only_this_project_even_after_restore_and_Reset_is_a_fresh_budget()
    {
        var visitor = await VisitorSession();
        var view = await Success(await Post(visitor, "start", new { }, 1));
        view = await Success(await Post(visitor, "welcome", Retry(view), 1));
        view = await Success(await Post(visitor, "welcome", Retry(view), 1));
        Assert.Equal("Exhausted", view.Activity.Last().Outcome);
        view = await Success(await Post(visitor, "restore", new { version = view.Onboarding!.Version }, 1));
        Assert.True(view.Onboarding!.WelcomeServiceAvailable);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "welcome", Retry(view), 1)).StatusCode);
        Assert.Equal(3, (await Saved(visitor, 1))!.Onboarding!.WelcomeAttempts.Length);
        var first = await Success(await Post(visitor, "start", new { }));
        Assert.Single(first.Onboarding!.WelcomeAttempts);
        Assert.Single(first.Outbox);
        Assert.True((await Send(visitor.Client, visitor.Token, "/api/session/reset", new { })).IsSuccessStatusCode);
        var freshProjects = (await visitor.Client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
        var fresh = await Success(await Send(visitor.Client, visitor.Token, $"/api/projects/{freshProjects[1].Id}/start", new { }));
        Assert.NotEqual(view.Onboarding.Id, fresh.Onboarding!.Id);
        Assert.Single(fresh.Onboarding.WelcomeAttempts);
    }

    [Fact]
    public async Task Publish_requires_readiness_and_parallel_replays_reuse_one_frozen_register_entry()
    {
        var visitor = await VisitorSession();
        var started = await Success(await Post(visitor, "start", new { }));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(visitor, "publish", new { })).StatusCode);
        Assert.Null((await Saved(visitor))!.RegisterEntry);
        var approved = await AllApproved(visitor);
        var ready = await Success(await Post(visitor, "ready", new { version = approved.Onboarding!.Version }));
        Assert.Null(ready.RegisterEntry);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Post(visitor, "publish", new { })));
        var views = await Task.WhenAll(responses.Select(Success));
        var saved = (await Saved(visitor))!;
        Assert.NotNull(saved.RegisterEntry);
        Assert.Single(saved.Activity, x => x.Action == "Simulated handoff confirmed");
        Assert.Equal(ready.Onboarding!.ReadyAt, saved.Onboarding!.ReadyAt);
        Assert.Equal(JsonSerializer.Serialize(ready.Brief), JsonSerializer.Serialize(saved.RegisterEntry.Brief));
        Assert.All(views, view => Assert.Equal(saved.RegisterEntry.Id, view.RegisterEntry!.Id));
        // A missing confirmation response is resolved by a read and replay, with the original reference/time.
        var replay = await Success(await Post(visitor, "publish", new { }));
        Assert.Equal(saved.RegisterEntry.Reference, replay.RegisterEntry!.Reference);
        Assert.Equal(saved.RegisterEntry.ConfirmedAt, replay.RegisterEntry.ConfirmedAt);
        Assert.Equal(saved.Activity, replay.Activity);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        Assert.Equal(1, await db.RegisterEntries.CountAsync(x => x.OnboardingId == started.Onboarding!.Id));
    }

    // Real PostgreSQL failures at the final Activity write prove the whole transaction rolls back.
    // Each trigger is confined to this disposable database and removed in finally.
    private async Task ActivityFailure(string? action)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        var sql = action is null ? """
            DROP TRIGGER IF EXISTS reject_activity ON "ActivityEvents";
            DROP FUNCTION IF EXISTS reject_activity();
            """ : $$"""
            CREATE FUNCTION reject_activity() RETURNS trigger LANGUAGE plpgsql AS $test$
            BEGIN
                IF NEW."Action" = '{{action}}' THEN RAISE EXCEPTION 'Intentional rollback test'; END IF;
                RETURN NEW;
            END; $test$;
            CREATE TRIGGER reject_activity BEFORE INSERT ON "ActivityEvents"
                FOR EACH ROW EXECUTE FUNCTION reject_activity();
            """;
        // action is a fixed test-owned constant, never a request value.
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Failed_initial_welcome_transaction_leaves_setup_saved_and_explicit_retry_can_continue()
    {
        var visitor = await VisitorSession();
        await ActivityFailure("Welcome message prepared");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(visitor, "start", new { })).StatusCode);
            var saved = (await Saved(visitor))!;
            Assert.NotNull(saved.Onboarding);
            Assert.Equal(3, saved.Items.Length);
            Assert.Empty(saved.Onboarding.WelcomeAttempts);
            Assert.Empty(saved.Outbox);
            Assert.Single(saved.Activity);
            Assert.Empty((await Success(await Post(visitor, "start", new { }))).Onboarding!.WelcomeAttempts);
        }
        finally { await ActivityFailure(null); }
        var pending = (await Saved(visitor))!;
        var prepared = await Success(await Post(visitor, "welcome", Retry(pending)));
        Assert.True(Assert.Single(prepared.Onboarding!.WelcomeAttempts).Prepared);
        Assert.Single(prepared.Outbox);
    }

    [Fact]
    public async Task Failed_retry_transaction_does_not_spend_an_attempt_or_create_a_message()
    {
        var visitor = await VisitorSession();
        var failed = await Success(await Post(visitor, "start", new { }, 1));
        var restored = await Success(await Post(visitor, "restore", new { version = failed.Onboarding!.Version }, 1));
        var command = Retry(restored);
        await ActivityFailure("Welcome message prepared");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(visitor, "welcome", command, 1)).StatusCode);
            var saved = (await Saved(visitor, 1))!;
            Assert.Single(saved.Onboarding!.WelcomeAttempts);
            Assert.Equal(restored.Onboarding!.Version, saved.Onboarding.Version);
            Assert.Empty(saved.Outbox);
            Assert.Equal(restored.Activity, saved.Activity);
        }
        finally { await ActivityFailure(null); }
        var retried = await Success(await Post(visitor, "welcome", command, 1));
        Assert.Equal(2, retried.Onboarding!.WelcomeAttempts.Length);
        Assert.Single(retried.Outbox);
    }

    [Fact]
    public async Task Failed_publish_transaction_keeps_ready_brief_without_false_confirmation()
    {
        var visitor = await VisitorSession();
        var approved = await AllApproved(visitor);
        var ready = await Success(await Post(visitor, "ready", new { version = approved.Onboarding!.Version }));
        await ActivityFailure("Simulated handoff confirmed");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(visitor, "publish", new { })).StatusCode);
            var saved = (await Saved(visitor))!;
            Assert.Null(saved.RegisterEntry);
            Assert.Equal(ready.Activity, saved.Activity);
            Assert.Equal(JsonSerializer.Serialize(ready.Brief), JsonSerializer.Serialize(saved.Brief));
        }
        finally { await ActivityFailure(null); }
        Assert.NotNull((await Success(await Post(visitor, "publish", new { }))).RegisterEntry);
    }

    [Fact]
    public async Task Upgrade_counts_an_existing_success_as_attempt_one_without_replacing_saved_work()
    {
        // A separate disposable fixture permits a genuine old-schema -> current-schema upgrade.
        var upgrade = new FoundationFixture();
        await upgrade.InitializeAsync();
        try
        {
            using var client = upgrade.Client();
            var token = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
            await Send(client, token, "/api/session", new { });
            var projects = (await client.GetFromJsonAsync<ProjectView[]>("/api/projects"))!;
            var original = await Success(await Send(client, token, $"/api/projects/{projects[0].Id}/start", new { }));
            await using (var scope = upgrade.Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FoundationDb>();
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync("20261018223038_MainWorkflow");
                await migrator.MigrateAsync();
            }
            var saved = (await client.GetFromJsonAsync<WorkflowView>($"/api/projects/{projects[0].Id}/onboarding"))!;
            Assert.True(Assert.Single(saved.Onboarding!.WelcomeAttempts).Prepared);
            Assert.Equal(1, saved.Onboarding.WelcomeAttempts[0].Number);
            Assert.Equal(original.Onboarding!.WelcomePreparedAt, saved.Onboarding.WelcomeAttempts[0].At);
            Assert.Equal(original.Onboarding.Id, saved.Onboarding.Id);
            Assert.Equal(original.Outbox[0].Id, saved.Outbox[0].Id);
            Assert.Equal(original.Activity, saved.Activity);
        }
        finally { await upgrade.DisposeAsync(); }
    }
}
