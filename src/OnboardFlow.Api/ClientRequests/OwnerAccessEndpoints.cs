using System.Net;
using Microsoft.AspNetCore.Antiforgery;

namespace OnboardFlow.Api;

public static class OwnerAccessEndpoints
{
    public static IApplicationBuilder UseOwnerAccess(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        // Normal startup renews only an existing, unrevoked grant. Public assets stay unpersonalized.
        if (HttpMethods.IsGet(http.Request.Method) && http.Request.Path.Equals("/api/csrf", StringComparison.OrdinalIgnoreCase))
        {
            var policy = http.RequestServices.GetRequiredService<OwnerAccessPolicy>();
            var pass = http.Request.Cookies[OwnerAccess.CookieName];
            if (await policy.Read(pass, http.RequestAborted) is { ExpiresAt: null } grant)
                http.Response.Cookies.Append(OwnerAccess.CookieName, pass!, Cookie(http.RequestServices.GetRequiredService<TimeProvider>(), grant));
        }
        await next(http);
    });

    public static void MapOwnerAccess(this WebApplication app)
    {
        app.MapGet("/api/owner-access", async (HttpContext http, OwnerAccessPolicy policy, TimeProvider clock, IAntiforgery antiforgery) =>
        {
            var pass = http.Request.Cookies[OwnerAccess.CookieName];
            var grant = await policy.Read(pass, http.RequestAborted);
            if (grant is { ExpiresAt: null }) http.Response.Cookies.Append(OwnerAccess.CookieName, pass!, Cookie(clock, grant));
            return Page(http, antiforgery, grant);
        });
        app.MapPost("/api/owner-access", async (HttpContext http, OwnerAccessPolicy policy, TimeProvider clock, IAntiforgery antiforgery) =>
        {
            if (!http.Request.HasFormContentType) return Results.BadRequest();
            var form = await http.Request.ReadFormAsync(http.RequestAborted);
            OwnerGrant? active = null;
            if (form["action"] == "disable")
            {
                var current = await policy.Read(http.Request.Cookies[OwnerAccess.CookieName], http.RequestAborted);
                if (current is not null) await policy.Revoke(current, http.RequestAborted);
                http.Response.Cookies.Delete(OwnerAccess.CookieName, Cookie(clock));
            }
            else if (form["action"] == "enable" && form["pass"].Count == 1
                && await policy.Read(form["pass"][0], http.RequestAborted) is { } grant)
            {
                http.Response.Cookies.Append(OwnerAccess.CookieName, form["pass"][0]!, Cookie(clock, grant));
                active = grant;
            }
            else return Results.Problem(statusCode: 403, title: "The owner pass is invalid, expired or revoked.");
            return Page(http, antiforgery, active);
        });
    }

    private static CookieOptions Cookie(TimeProvider clock, OwnerGrant? grant = null)
    {
        // Chrome limits persistent cookies to 400 days. Renew the browser cookie on normal startup;
        // the permanent signed grant itself has no calendar expiry.
        DateTimeOffset? expires = grant is null ? null : grant.ExpiresAt ?? clock.GetUtcNow().AddDays(400);
        return new()
        {
            Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true,
            Expires = expires, MaxAge = expires is null ? null : expires - clock.GetUtcNow()
        };
    }

    private static IResult Page(HttpContext http, IAntiforgery antiforgery, OwnerGrant? grant)
    {
        var token = antiforgery.GetAndStoreTokens(http);
        var protection = $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(token.FormFieldName)}\" value=\"{WebUtility.HtmlEncode(token.RequestToken)}\">";
        var status = grant?.ExpiresAt is { } expires ? $"Owner access enabled in this browser until {expires:yyyy-MM-dd} UTC."
            : "Permanent owner access enabled in this browser. The browser cookie renews automatically when you use the demo.";
        var content = grant is not null
                ? $"<p>{status}</p><p>The three-run limit does not apply. Shared spending and per-run safeguards still apply.</p><form method=\"post\" action=\"/api/owner-access\">{protection}<button name=\"action\" value=\"disable\">Disable owner access</button></form>"
                : $"<p>Activate a privately issued owner pass. Public visitors retain their three-run allowance.</p><form method=\"post\" action=\"/api/owner-access\">{protection}<label for=\"owner-pass\">Owner pass</label><input id=\"owner-pass\" name=\"pass\" type=\"password\" autocomplete=\"off\" maxlength=\"256\" required><button name=\"action\" value=\"enable\">Enable owner access</button></form>";
        return Results.Content($$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex,nofollow"><title>OnboardFlow owner access</title></head>
            <body><main><h1>OnboardFlow owner access</h1>
            {{content}}
            <p><a href="/">Return to demo</a></p></main></body></html>
            """, "text/html");
    }
}
