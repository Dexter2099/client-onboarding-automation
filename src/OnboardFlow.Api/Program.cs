using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnboardFlow.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
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
app.UseExceptionHandler(handler => handler.Run(async http =>
{
    var error = http.Features.Get<IExceptionHandlerFeature>()?.Error;
    var databaseUnavailable = error is NpgsqlException or DbUpdateException;
    await Results.Problem(statusCode: databaseUnavailable ? 503 : 500,
        title: databaseUnavailable ? "The workspace database is unavailable. Try loading again."
            : "The workspace could not complete this request.").ExecuteAsync(http);
}));
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
