using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

// Signed-pass verification remains pure; durable revocation is an independent policy.
public sealed class OwnerAccessPolicy(OwnerAccess verifier, FoundationDb db, TimeProvider clock)
{
    public async Task<OwnerGrant?> Read(string? pass, CancellationToken cancellation)
    {
        var grant = verifier.Read(pass);
        return grant is not null && !await db.RevokedOwnerGrants.AnyAsync(x => x.Id == grant.Id, cancellation)
            ? grant : null;
    }

    public Task Revoke(OwnerGrant grant, CancellationToken cancellation) => db.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO \"RevokedOwnerGrants\" (\"Id\", \"RevokedAt\") VALUES ({grant.Id}, {clock.GetUtcNow()}) ON CONFLICT (\"Id\") DO NOTHING", cancellation);
}
