using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnboardFlow.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiSafety();
const string cloudflareProtoHeader = "X-OnboardFlow-Proto";
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    if (builder.Configuration.GetValue<bool>("ReverseProxy:UseCloudflareVisitor"))
        options.ForwardedProtoHeaderName = cloudflareProtoHeader;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddDbContext<FoundationDb>((services, options) => options.UseNpgsql(
    services.GetRequiredService<IConfiguration>().GetConnectionString("OnboardFlow")
    ?? throw new InvalidOperationException("Set ConnectionStrings__OnboardFlow; see HARNESS.md.")));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

var app = builder.Build();
var knownProxies = (app.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    .Select(IPAddress.Parse).ToArray();
var useCloudflareVisitor = app.Configuration.GetValue<bool>("ReverseProxy:UseCloudflareVisitor");
if (useCloudflareVisitor) app.Use(async (http, next) =>
{
    // Opt in only after verifying that this ingress overwrites visitor-supplied CF-Visitor.
    // Never accept a client-supplied derived header or an unconfigured forwarding peer.
    http.Request.Headers.Remove(cloudflareProtoHeader);
    var visitor = http.Request.Headers["CF-Visitor"];
    if (http.Connection.RemoteIpAddress is { } peer && knownProxies.Contains(peer)
        && visitor.Count == 1 && visitor[0] is { Length: > 0 and <= 128 } value)
    {
        try
        {
            using var json = JsonDocument.Parse(value);
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.EnumerateObject().Count() == 1
                && json.RootElement.TryGetProperty("scheme", out var scheme) && scheme.ValueKind == JsonValueKind.String
                && scheme.GetString() is "http" or "https")
                http.Request.Headers[cloudflareProtoHeader] = scheme.GetString();
        }
        catch (JsonException) { /* Missing or invalid scheme evidence must fail closed. */ }
    }
    await next(http);
});
app.UseForwardedHeaders();
app.UseExceptionHandler(handler => handler.Run(async http =>
{
    var error = http.Features.Get<IExceptionHandlerFeature>()?.Error;
    var databaseUnavailable = error is NpgsqlException or DbUpdateException;
    var badRequest = error as BadHttpRequestException;
    await Results.Problem(statusCode: badRequest?.StatusCode ?? (databaseUnavailable ? 503 : 500),
        title: badRequest is not null ? "The request is invalid or too large."
        : databaseUnavailable ? "The workspace database is unavailable. Try loading again."
            : "The workspace could not complete this request.").ExecuteAsync(http);
}));
app.UseApiSafety();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api"))
    {
        http.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsPost(http.Request.Method))
        {
            try
            {
                await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                await Results.Problem(statusCode: 403,
                    title: "Refresh the workspace before making this change.").ExecuteAsync(http);
                return;
            }
        }
    }
    await next(http);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/csrf", (HttpContext http, IAntiforgery antiforgery) =>
    Results.Ok(new { token = antiforgery.GetAndStoreTokens(http).RequestToken }));
app.MapSandboxEndpoints();
app.MapWorkflowEndpoints();
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Unknown API route."));
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
