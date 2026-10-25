var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
var app = builder.Build();
app.MapGet("/", () => Results.Text("OnboardFlow hosting verification. No client data or AI inference."));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/release.json", () => Results.File("/app/release.json", "application/json"));
app.MapGet("/inspect", (HttpContext http) => Results.Json(new
{
    scheme = http.Request.Scheme,
    peer = http.Connection.RemoteIpAddress?.ToString(),
    headers = new[] { "X-Forwarded-Proto", "X-Forwarded-Host", "Forwarded", "CF-Visitor", "X-OnboardFlow-Proto" }
        .ToDictionary(name => name, name => http.Request.Headers[name].ToString()[..Math.Min(200, http.Request.Headers[name].ToString().Length)])
}));
app.Run();
