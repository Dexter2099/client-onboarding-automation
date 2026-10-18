using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public static class SandboxEndpoints
{
    public const string CookieName = "onboardflow.sandbox";

    public static void MapSandboxEndpoints(this WebApplication app)
    {
        app.MapGet("/api/session", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await FindSession(http, db, false);
            return Active(session, clock) ? Results.Ok(View(session!)) : Unavailable(http);
        });

        app.MapPost("/api/session", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var existing = await FindSession(http, db, true);
            if (Active(existing, clock))
            {
                await transaction.CommitAsync(http.RequestAborted);
                return Results.Ok(View(existing!));
            }

            var (session, token) = NewSandbox(clock);
            db.SandboxSessions.Add(session);
            await DeleteExpired(db, clock, http.RequestAborted);
            await db.SaveChangesAsync(http.RequestAborted);
            if (!Active(session, clock)) return Unavailable(http);
            await transaction.CommitAsync(http.RequestAborted);
            SetCookie(http, session, token, app.Environment.IsDevelopment());
            return Results.Created("/api/session", View(session));
        });

        app.MapPost("/api/session/reset", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var previous = await FindSession(http, db, true);
            if (!Active(previous, clock)) return Unavailable(http);

            var (session, token) = NewSandbox(clock);
            db.SandboxSessions.Remove(previous!);
            db.SandboxSessions.Add(session);
            await db.SaveChangesAsync(http.RequestAborted);
            if (!Active(previous, clock)) return Unavailable(http);
            await transaction.CommitAsync(http.RequestAborted);
            SetCookie(http, session, token, app.Environment.IsDevelopment());
            return Results.Ok(View(session));
        });

        app.MapGet("/api/projects", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await FindSession(http, db, false);
            if (!Active(session, clock)) return Unavailable(http);
            var projects = await db.Projects.AsNoTracking().Where(x => x.SandboxSessionId == session!.Id)
                .OrderBy(x => x.Reference).Select(x => new ProjectView(x.Id, x.Reference, x.Name,
                    x.ClientName, x.ClientContact, x.Owner, x.Service, x.SignedPages))
                .ToArrayAsync(http.RequestAborted);
            return Active(session, clock) ? Results.Ok(projects) : Unavailable(http);
        });

        app.MapGet("/api/projects/{id:guid}", async (Guid id, HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await FindSession(http, db, false);
            if (!Active(session, clock)) return Unavailable(http);
            var project = await db.Projects.AsNoTracking()
                .Where(x => x.Id == id && x.SandboxSessionId == session!.Id)
                .Select(x => new ProjectView(x.Id, x.Reference, x.Name, x.ClientName,
                    x.ClientContact, x.Owner, x.Service, x.SignedPages))
                .SingleOrDefaultAsync(http.RequestAborted);
            if (!Active(session, clock)) return Unavailable(http);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });
    }

    internal static async Task<SandboxSession?> FindSession(HttpContext http, FoundationDb db, bool forUpdate)
    {
        if (!http.Request.Cookies.TryGetValue(CookieName, out var token) ||
            token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        return forUpdate
            ? await db.SandboxSessions.FromSqlInterpolated(
                $"SELECT * FROM \"SandboxSessions\" WHERE \"TokenHash\" = {hash} FOR UPDATE")
                .SingleOrDefaultAsync(http.RequestAborted)
            : await db.SandboxSessions.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash,
                http.RequestAborted);
    }

    internal static bool Active(SandboxSession? session, TimeProvider clock) =>
        session is not null && clock.GetUtcNow() < session.ExpiresAt;

    internal static IResult Unavailable(HttpContext http) => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "This sandbox is unavailable. Start fresh to continue.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = http.Request.Cookies.ContainsKey(CookieName) ? "sandbox_unavailable" : "sandbox_missing"
        });

    private static SessionView View(SandboxSession session) => new(session.Id, session.CreatedAt, session.ExpiresAt);

    private static (SandboxSession Session, string Token) NewSandbox(TimeProvider clock)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        // PostgreSQL timestamps have microsecond precision; keep initial/loaded responses identical.
        now = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        var session = new SandboxSession
        {
            Id = Guid.NewGuid(),
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };
        session.Projects = [SeedProject("OF-1042", "Website redesign"), SeedProject("OF-1043", "Website refresh")];
        return (session, token);
    }

    private static Project SeedProject(string reference, string name) => new()
    {
        Id = Guid.NewGuid(), Reference = reference, Name = name, ClientName = "Juniper Studio",
        ClientContact = "Maya Ellis", Owner = "Alex Morgan", Service = "Website design",
        SignedPages = ["Home", "About", "Services", "Projects", "Contact"]
    };

    private static void SetCookie(HttpContext http, SandboxSession session, string token, bool development) =>
        http.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true, Secure = !development, SameSite = SameSiteMode.Strict,
            Path = "/", Expires = session.ExpiresAt, MaxAge = TimeSpan.FromHours(24), IsEssential = true
        });

    private static async Task DeleteExpired(FoundationDb db, TimeProvider clock, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var expired = await db.SandboxSessions.Where(x => x.ExpiresAt <= now).OrderBy(x => x.ExpiresAt)
            .Take(20).Select(x => x.Id).ToArrayAsync(cancellation);
        if (expired.Length > 0)
            await db.SandboxSessions.Where(x => expired.Contains(x.Id) && x.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellation);
    }
}
