using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

internal static class SandboxSessionAccess
{
    internal static async Task<SandboxSession?> Find(string? token, FoundationDb db, bool forUpdate, CancellationToken cancellationToken)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        return forUpdate
            ? await db.SandboxSessions.FromSqlInterpolated(
                $"SELECT * FROM \"SandboxSessions\" WHERE \"TokenHash\" = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await db.SandboxSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash,
                cancellationToken);
    }

    internal static bool Active(SandboxSession? session, TimeProvider clock) =>
        session is not null && clock.GetUtcNow() < session.ExpiresAt;

}
