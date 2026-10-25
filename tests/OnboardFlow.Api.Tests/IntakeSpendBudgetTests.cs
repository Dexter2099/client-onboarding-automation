using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

[Trait("Category", "Integration")]
public sealed class IntakeSpendBudgetTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private async Task<T> WithBudget<T>(Func<IIntakeSpendBudget, FoundationDb, Task<T>> action)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IIntakeSpendBudget>(), scope.ServiceProvider.GetRequiredService<FoundationDb>());
    }
    [Fact]
    public async Task Concurrent_instances_cannot_reserve_the_same_remaining_money()
    {
        var project = "concurrency-" + Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 14).Select(_ => WithBudget(async (budget, _) =>
        {
            try { await budget.Reserve(project, 900000, default); return true; }
            catch (IntakeException error) when (error.Status == 429) { return false; }
        })));
        Assert.Equal(5, results.Count(x => x));
        Assert.Equal(IntakeSpendBudget.MonthlyLimitMicros, await WithBudget((_, db) => db.IntakeSpendReservations.Where(x => x.Scope == project).SumAsync(x => x.AccountedMicros)));
    }
    [Fact]
    public async Task Uncertain_reservations_survive_new_scopes_and_only_a_new_month_restores_allowance()
    {
        var project = "uncertain-" + Guid.NewGuid();
        var now = fixture.Clock.Now;
        try
        {
            await WithBudget(async (budget, _) => await budget.Reserve(project, IntakeSpendBudget.MonthlyLimitMicros, default));
            await Assert.ThrowsAsync<IntakeException>(() => WithBudget(async (budget, _) => await budget.Reserve(project, 1, default)));
            fixture.Clock.Now = now.AddMonths(1);
            await WithBudget(async (budget, _) => await budget.Reserve(project, 1, default));
            Assert.Equal(2, await WithBudget((_, db) => db.IntakeSpendReservations.CountAsync(x => x.Scope == project)));
        }
        finally { fixture.Clock.Now = now; }
    }
    [Fact]
    public async Task Settlement_releases_unused_hold_once_and_duplicate_completion_cannot_refund_again()
    {
        var project = "settle-" + Guid.NewGuid();
        var id = await WithBudget(async (budget, _) => await budget.Reserve(project, 900000, default));
        await WithBudget(async (budget, _) => { await budget.Settle(project, id, 300000, default); return true; });
        await WithBudget(async (budget, _) => { await budget.Settle(project, id, 1, default); return true; });
        Assert.Equal(300000, await WithBudget((_, db) => db.IntakeSpendReservations.Where(x => x.Id == id).Select(x => x.AccountedMicros).SingleAsync()));
    }
    [Fact]
    public async Task Unexpected_usage_overrun_stops_further_inference_for_budget_review()
    {
        var project = "overrun-" + Guid.NewGuid();
        var id = await WithBudget(async (budget, _) => await budget.Reserve(project, 1000, default));
        await Assert.ThrowsAsync<IntakeException>(() => WithBudget(async (budget, _) => { await budget.Settle(project, id, 1001, default); return true; }));
        await Assert.ThrowsAsync<IntakeException>(() => WithBudget(async (budget, _) => await budget.Reserve(project, 1, default)));
    }
}
