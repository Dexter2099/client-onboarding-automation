using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class NetworkRunAllowance(FoundationDb db, TimeProvider clock, IConfiguration configuration)
{
    public async Task Consume(string? network, CancellationToken cancellation)
    {
        // Local/test hosts have no signed gateway. Production requires the verified caller feature.
        if (network is null) return;
        if (network.Length != 64 || !network.All(Uri.IsHexDigit))
            throw new IntakeException(503, "The demo caller could not be verified.");
        var limit = Math.Clamp(configuration.GetValue("Abuse:PublicStartsPerNetworkPerDay", 12), 1, 10000);
        var now = clock.GetUtcNow();
        var day = new DateOnly(now.Year, now.Month, now.Day);
        // One atomic upsert inside IntakeStore's transaction: stale/repeated commands do not consume
        // allowance, and separate anonymous workspaces cannot race past the shared daily bound.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "NetworkRunUsage" ("Network", "Day", "Starts") VALUES ({network}, {day}, 1)
            ON CONFLICT ("Network", "Day") DO UPDATE SET "Starts" = "NetworkRunUsage"."Starts" + 1
            WHERE "NetworkRunUsage"."Starts" < {limit}
            """, cancellation);
        if (changed == 0) throw new IntakeException(429,
            "This network's daily AI demo allowance has been used. Saved work and manual completion are available.");
    }
}
