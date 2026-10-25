using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class IntakeSpendReservation
{
    public Guid Id { get; set; }
    public required string Scope { get; set; }
    public DateOnly Month { get; set; }
    public long ReservedMicros { get; set; }
    public long AccountedMicros { get; set; }
    public bool Settled { get; set; }
}
public sealed partial class FoundationDb
{
    public DbSet<IntakeSpendReservation> IntakeSpendReservations => Set<IntakeSpendReservation>();
    private static void ConfigureIntakeSpend(ModelBuilder model)
    {
        var budget = model.Entity<IntakeSpendReservation>();
        budget.Property(x => x.Scope).HasMaxLength(80);
        budget.HasIndex(x => new { x.Scope, x.Month });
        budget.ToTable(table => table.HasCheckConstraint("CK_IntakeSpend_Nonnegative",
            "\"ReservedMicros\" > 0 AND \"AccountedMicros\" >= 0"));
        // Deliberately independent of sandboxes: reset/expiry must never refund global spend.
    }
}
public interface IIntakeSpendBudget
{
    Task<Guid> Reserve(string scope, long maximumMicros, CancellationToken cancellation);
    Task Settle(string scope, Guid reservation, long costMicros, CancellationToken cancellation);
}
public sealed class IntakeSpendBudget([FromKeyedServices("intake-spend")] FoundationDb db, TimeProvider clock) : IIntakeSpendBudget
{
    public const long MonthlyLimitMicros = 4_500_000; // US$4.50; provider hard limit is US$5.
    public async Task<Guid> Reserve(string scope, long maximumMicros, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > 80 || maximumMicros <= 0 || maximumMicros > MonthlyLimitMicros)
            throw new IntakeException(503, "The AI spending policy is invalid.");
        var now = clock.GetUtcNow();
        var month = new DateOnly(now.Year, now.Month, 1);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        // Shared transaction lock across API instances. Never infer while holding this lock.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(904202610)", cancellation);
        var committed = await db.IntakeSpendReservations.Where(x => x.Scope == scope && x.Month == month)
            .SumAsync(x => x.AccountedMicros, cancellation);
        if (committed > MonthlyLimitMicros - maximumMicros)
            throw new IntakeException(429, "This month's AI demo allowance has been used. Saved work and manual completion are available.");
        var row = new IntakeSpendReservation
        {
            Id = Guid.NewGuid(), Scope = scope, Month = month,
            ReservedMicros = maximumMicros, AccountedMicros = maximumMicros
        };
        db.IntakeSpendReservations.Add(row);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return row.Id;
    }
    public async Task Settle(string scope, Guid reservation, long costMicros, CancellationToken cancellation)
    {
        if (costMicros < 0) throw new IntakeException(503, "The provider returned invalid cost information.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(904202610)", cancellation);
        var row = await db.IntakeSpendReservations.SingleAsync(x => x.Id == reservation && x.Scope == scope, cancellation);
        // A duplicate or late completion cannot refund an already-settled reservation.
        if (!row.Settled)
        {
            row.AccountedMicros = costMicros <= row.ReservedMicros ? costMicros : MonthlyLimitMicros;
            row.Settled = true;
            await db.SaveChangesAsync(cancellation);
        }
        await transaction.CommitAsync(cancellation);
        if (costMicros > row.ReservedMicros)
            throw new IntakeException(503, "Provider usage exceeded its reservation. AI has been stopped for budget review.");
    }
}
