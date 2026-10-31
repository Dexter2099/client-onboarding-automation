using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

public sealed class IntakePersistenceTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private sealed class Savepoints : DbTransactionInterceptor
    {
        public int Created, Released;
        public override ValueTask<InterceptionResult> CreatingSavepointAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { Created++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult> ReleasingSavepointAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { Released++; return ValueTask.FromResult(result); }
    }
    private sealed class FailAfterWrite : SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => Fail
            ? throw new InvalidOperationException("Deterministic failure after SQL, before commit.") : ValueTask.FromResult(result);
    }
    [Fact]
    public async Task Saves_preserve_atomic_rollback_and_receipts_without_nested_savepoints()
    {
        var points = new Savepoints();
        var failure = new FailAfterWrite();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FoundationDb>(options => options.AddInterceptors(points, failure))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/csrf"))!["token"];
        async Task<HttpResponseMessage> Post(string path, object data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(data) };
            request.Headers.Add("X-CSRF-TOKEN", csrf);
            return await client.SendAsync(request);
        }
        (await Post("/api/session", new { })).EnsureSuccessStatusCode();
        var initial = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        var command = new IntakeCommand(Guid.NewGuid(), initial.Version, "edit", "legal", "Fictional latency review");
        points.Created = points.Released = 0;
        (await Post("/api/intake/command", command)).EnsureSuccessStatusCode();
        Assert.Equal(0, points.Created);
        Assert.Equal(0, points.Released);
        var saved = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        failure.Fail = true;
        var failedCommand = new IntakeCommand(Guid.NewGuid(), saved.Version, "edit", "legal", "Must roll back");
        Assert.Equal(HttpStatusCode.InternalServerError, (await Post("/api/intake/command", failedCommand)).StatusCode);
        var restored = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        Assert.Equal(saved.Version, restored.Version);
        Assert.Equal("Fictional latency review", restored.Answers["legal"].Value);
        failure.Fail = false;
        (await Post("/api/intake/command", failedCommand)).EnsureSuccessStatusCode();
        var committed = (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!;
        (await Post("/api/intake/command", failedCommand)).EnsureSuccessStatusCode();
        Assert.Equal(committed.Version, (await client.GetFromJsonAsync<IntakeView>("/api/intake/"))!.Version);
    }
}
