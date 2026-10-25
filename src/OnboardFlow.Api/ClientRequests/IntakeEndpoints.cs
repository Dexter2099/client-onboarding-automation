using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public static class IntakeEndpoints
{
    public static void AddIntake(this IServiceCollection services)
    {
        services.AddSingleton<IntakeSpecification>();
        services.AddScoped<IntakeStore>();
        services.AddScoped<IntakeAgentService>();
        services.AddScoped<IIntakeSpendBudget, IntakeSpendBudget>();
        services.AddKeyedScoped<FoundationDb>("intake-spend", (provider, _) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connection = config["Intake:SpendConnectionString"] ?? config.GetConnectionString("OnboardFlow")
                ?? throw new InvalidOperationException("The spending ledger needs PostgreSQL.");
            return new FoundationDb(new DbContextOptionsBuilder<FoundationDb>().UseNpgsql(connection).Options);
        });
        services.AddHttpClient<OllamaIntakeClient>(http =>
        {
            http.BaseAddress = new Uri("http://127.0.0.1:11434/");
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<CloudflareIntakeClient>(http => http.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<OpenAiIntakeClient>(http => http.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IIntakeModelClient>(provider =>
        {
            var selected = provider.GetRequiredService<IConfiguration>()["Intake:Provider"] ?? "Ollama";
            return selected switch
            {
                "Ollama" => provider.GetRequiredService<OllamaIntakeClient>(),
                "Cloudflare" => provider.GetRequiredService<CloudflareIntakeClient>(),
                "OpenAI" => provider.GetRequiredService<OpenAiIntakeClient>(),
                _ => throw new InvalidOperationException("Intake:Provider must be Ollama, Cloudflare or OpenAI.")
            };
        });
    }
    public static void MapIntake(this WebApplication app)
    {
        // Production requires an explicit, separately budgeted hosted-provider rollout.
        if (app.Environment.IsProduction())
        {
            if (!app.Configuration.GetValue<bool>("Intake:Enabled")) return;
            if (app.Configuration["Intake:Provider"] != "OpenAI"
                || !app.Configuration.GetValue<bool>("Intake:OpenAI:BudgetVerified")
                || string.IsNullOrWhiteSpace(app.Configuration["Intake:OpenAI:ApiKey"])
                || app.Configuration["Intake:OpenAI:ProjectId"] != "proj_ducK0I8812r3DwpD89brnzC2")
                throw new InvalidOperationException("Production intake requires the verified, capped OnboardFlow OpenAI project.");
        }
        var routes = app.MapGroup("/api/intake");
        routes.MapGet("/", (HttpContext http, IntakeStore store) =>
            Respond(() => store.Read(Token(http), http.RequestAborted)));
        routes.MapPost("/command", (IntakeCommand command, HttpContext http, IntakeStore store) =>
            Respond(() => store.Command(Token(http), command, http.RequestAborted)));
        routes.MapPost("/agent", (AgentCommand command, HttpContext http, IntakeAgentService agent) =>
            Respond(() => agent.Run(Token(http), command, http.RequestAborted)));
    }
    private static string? Token(HttpContext http) => http.Request.Cookies[SandboxEndpoints.CookieName];
    private static async Task<IResult> Respond(Func<Task<IntakeSnapshot>> action)
    {
        try { return Results.Ok(IntakeStore.View(await action())); }
        catch (IntakeException error) { return Results.Problem(statusCode: error.Status, title: error.Message); }
    }
}
