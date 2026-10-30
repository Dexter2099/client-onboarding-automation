namespace OnboardFlow.Api;

public sealed class LivenessStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (http, following) =>
        {
            // Only this fixed GET response precedes host/signature checks; all other routes keep them.
            // Liveness must not depend on PostgreSQL, inference, cookies or forwarded headers.
            if (HttpMethods.IsGet(http.Request.Method) && http.Request.Path == "/health")
            {
                http.Response.Headers.CacheControl = "no-store";
                await http.Response.WriteAsJsonAsync(new { status = "ok" });
                return;
            }
            await following(http);
        });
        next(app);
    };
}
