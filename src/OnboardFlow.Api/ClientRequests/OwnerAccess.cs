using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.WebUtilities;

namespace OnboardFlow.Api;

public sealed record OwnerGrant(DateTimeOffset? ExpiresAt);

public sealed class OwnerAccess(IConfiguration configuration, TimeProvider clock)
{
    public const string CookieName = "__Host-onboardflow.owner";
    // Domain separation prevents an ordinary signed gateway request becoming an owner pass.
    internal const string Purpose = "onboardflow-owner-runs-v1";
    internal const string PermanentPurpose = "onboardflow-owner-runs-v2";
    public bool HasAccess(HttpContext http) => Read(http.Request.Cookies[CookieName]) is not null;

    public OwnerGrant? Read(string? pass)
    {
        if (pass is null || pass.Length > 256) return null;
        var parts = pass.Split('.');
        if (parts.Length != 4 || parts[2].Length != 32 || !parts[2].All(Uri.IsHexDigit)) return null;
        try
        {
            DateTimeOffset? expires = null;
            string purpose;
            if (parts[0] == "v2" && parts[1] == "permanent") purpose = PermanentPurpose;
            else if (parts[0] == "v1" && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                expires = DateTimeOffset.FromUnixTimeSeconds(seconds);
                var now = clock.GetUtcNow();
                if (expires <= now || expires > now.AddDays(90).AddMinutes(5)) return null;
                purpose = Purpose;
            }
            else return null;
            var origin = configuration["VerifiedEdge:PublicOrigin"] ?? "";
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var address) || address.Scheme != "https"
                || address.GetLeftPart(UriPartial.Authority) != origin) return null;
            var signature = WebEncoders.Base64UrlDecode(parts[3]);
            if (signature.Length != 64) return null;
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(Convert.FromBase64String(configuration["OwnerAccess:PublicKey"] ?? ""), out _);
            var canonical = string.Join('\n', purpose, origin, parts[1], parts[2]);
            return verifier.KeySize == 256 && verifier.VerifyData(Encoding.UTF8.GetBytes(canonical), signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation) ? new(expires) : null;
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException) { return null; }
    }

    internal CookieOptions Cookie(OwnerGrant? grant = null)
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
}

public static class OwnerAccessEndpoints
{
    public static IApplicationBuilder UseOwnerAccess(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        // Refresh only private, no-store startup/status responses. Never personalize cached assets.
        if (HttpMethods.IsGet(http.Request.Method)
            && (http.Request.Path.Equals("/api/csrf", StringComparison.OrdinalIgnoreCase)
                || http.Request.Path.Equals("/api/owner-access", StringComparison.OrdinalIgnoreCase)))
        {
            var owner = http.RequestServices.GetRequiredService<OwnerAccess>();
            var pass = http.Request.Cookies[OwnerAccess.CookieName];
            if (owner.Read(pass) is { ExpiresAt: null } grant)
                http.Response.Cookies.Append(OwnerAccess.CookieName, pass!, owner.Cookie(grant));
        }
        await next(http);
    });

    public static void MapOwnerAccess(this WebApplication app)
    {
        // Unlinked operator page; no change to the public demo's screens or navigation.
        app.MapGet("/api/owner-access", (HttpContext http, OwnerAccess owner, IAntiforgery antiforgery) =>
            Page(http, antiforgery, owner.Read(http.Request.Cookies[OwnerAccess.CookieName])));
        app.MapPost("/api/owner-access", async (HttpContext http, OwnerAccess owner, IAntiforgery antiforgery) =>
        {
            // The existing API middleware validates CSRF, including native form tokens.
            if (!http.Request.HasFormContentType) return Results.BadRequest();
            var form = await http.Request.ReadFormAsync(http.RequestAborted);
            OwnerGrant? active = null;
            if (form["action"] == "disable")
                http.Response.Cookies.Delete(OwnerAccess.CookieName, owner.Cookie());
            else if (form["action"] == "enable" && form["pass"].Count == 1 && owner.Read(form["pass"][0]) is { } grant)
            {
                http.Response.Cookies.Append(OwnerAccess.CookieName, form["pass"][0]!, owner.Cookie(grant));
                active = grant;
            }
            else return Results.Problem(statusCode: 403, title: "The owner pass is invalid or expired.");
            // Return the status directly: the signed gateway deliberately rejects origin redirects.
            return Page(http, antiforgery, active);
        });
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
