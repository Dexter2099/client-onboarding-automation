using System.Globalization;
using System.Diagnostics;
using System.Threading.RateLimiting;

namespace OnboardFlow.Api;

public static class ApiSafety
{
    private const int MaxBodyBytes = 16 * 1024;
    private const string BrowserPolicy = "default-src 'none'; script-src 'self'; script-src-attr 'none'; "
        + "style-src 'self'; style-src-attr 'none'; img-src 'self'; font-src 'self'; media-src 'self'; connect-src 'self'; "
        + "base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'";

    public static void AddApiSafety(this WebApplicationBuilder builder)
    {
        // Initial rollout pins only this host. Do not preload a provider-owned domain.
        builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(1));
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxBodyBytes);
        builder.Services.AddRateLimiter(options =>
        {
            // Global bounds avoid trusting visitor-supplied IP/cookie partition keys.
            // This small public demo deliberately rejects excess work instead of queuing it.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(http => IsApi(http)
                    ? RateLimitPartition.GetConcurrencyLimiter("api", _ => new() { PermitLimit = 6, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("static")),
                PartitionedRateLimiter.Create<HttpContext, string>(http => IsApi(http)
                    ? RateLimitPartition.GetFixedWindowLimiter("api", _ => new()
                    {
                        PermitLimit = builder.Configuration.GetValue("ApiLimits:RequestsPerMinute", 240),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })
                    : RateLimitPartition.GetNoLimiter("static")));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429,
                    title: "The demo is busy. Wait briefly, then load the saved workspace.").ExecuteAsync(context.HttpContext);
            };
        });
    }

    public static void UseApiSafety(this WebApplication app)
    {
        app.Use(async (http, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.XFrameOptions = "DENY";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";
            http.Response.OnStarting(() =>
            {
                // Numeric application time only: distinguish origin work from transport waits.
                http.Response.Headers["Server-Timing"] = "app;dur=" +
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
                // Apply after static/error handling; preserve the document through proxy transformations.
                http.Response.Headers.ContentSecurityPolicy = BrowserPolicy;
                if (http.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    http.Response.Headers.CacheControl = IsApi(http) ? "no-store" : "no-cache, no-transform";
                return Task.CompletedTask;
            });
            if (IsApi(http))
            {
                http.Response.Headers.CacheControl = "no-store";
                if (http.Request.ContentLength > MaxBodyBytes)
                {
                    await Results.Problem(statusCode: 413, title: "Keep this demo request under 16 KiB.").ExecuteAsync(http);
                    return;
                }
            }
            await next(http);
        });
        app.UseRateLimiter();
    }

    private static bool IsApi(HttpContext http) => http.Request.Path.StartsWithSegments("/api");
}
