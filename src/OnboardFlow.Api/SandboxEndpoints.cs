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
            var session = await SandboxSessionAccess.Find(http.Request.Cookies[CookieName], db, false, http.RequestAborted);
            return SandboxSessionAccess.Active(session, clock) ? Results.Ok(View(session!)) : Unavailable(http);
        });

        app.MapPost("/api/session", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var existing = await SandboxSessionAccess.Find(http.Request.Cookies[CookieName], db, true, http.RequestAborted);
            if (SandboxSessionAccess.Active(existing, clock))
            {
                await transaction.CommitAsync(http.RequestAborted);
                return Results.Ok(View(existing!));
            }

            var (session, token) = NewSandbox(clock);
            db.SandboxSessions.Add(session);
            await DeleteExpired(db, clock, http.RequestAborted);
            await db.SaveChangesAsync(http.RequestAborted);
            if (!SandboxSessionAccess.Active(session, clock)) return Unavailable(http);
            await transaction.CommitAsync(http.RequestAborted);
            SetCookie(http, session, token, app.Environment.IsDevelopment());
            return Results.Created("/api/session", View(session));
        });

        app.MapPost("/api/session/reset", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var previous = await SandboxSessionAccess.Find(http.Request.Cookies[CookieName], db, true, http.RequestAborted);
            if (!SandboxSessionAccess.Active(previous, clock)) return Unavailable(http);

            var (session, token) = NewSandbox(clock);
            db.SandboxSessions.Remove(previous!);
            db.SandboxSessions.Add(session);
            await db.SaveChangesAsync(http.RequestAborted);
            if (!SandboxSessionAccess.Active(previous, clock)) return Unavailable(http);
            await transaction.CommitAsync(http.RequestAborted);
            SetCookie(http, session, token, app.Environment.IsDevelopment());
            return Results.Ok(View(session));
        });

        app.MapGet("/api/projects", async (HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await SandboxSessionAccess.Find(http.Request.Cookies[CookieName], db, false, http.RequestAborted);
            if (!SandboxSessionAccess.Active(session, clock)) return Unavailable(http);
            var projects = await db.Projects.AsNoTracking().Where(x => x.SandboxSessionId == session!.Id)
                .OrderBy(x => x.Reference).Select(x => new ProjectView(x.Id, x.Reference, x.Name,
                    x.ClientName, x.ClientContact, x.Owner, x.Service, x.SignedPages))
                .ToArrayAsync(http.RequestAborted);
            return SandboxSessionAccess.Active(session, clock) ? Results.Ok(projects) : Unavailable(http);
        });

        app.MapGet("/api/projects/{id:guid}", async (Guid id, HttpContext http, FoundationDb db, TimeProvider clock) =>
        {
            var session = await SandboxSessionAccess.Find(http.Request.Cookies[CookieName], db, false, http.RequestAborted);
            if (!SandboxSessionAccess.Active(session, clock)) return Unavailable(http);
            var project = await db.Projects.AsNoTracking()
                .Where(x => x.Id == id && x.SandboxSessionId == session!.Id)
                .Select(x => new ProjectView(x.Id, x.Reference, x.Name, x.ClientName,
                    x.ClientContact, x.Owner, x.Service, x.SignedPages))
                .SingleOrDefaultAsync(http.RequestAborted);
            if (!SandboxSessionAccess.Active(session, clock)) return Unavailable(http);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });
    }

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
