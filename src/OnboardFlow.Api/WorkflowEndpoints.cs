namespace OnboardFlow.Api;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects/{id:guid}/onboarding", async (Guid id, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Read(id, Token(http), http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/start", async (Guid id, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Start(id, Token(http), http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/reply", async (Guid id, SampleReplyRequest request, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Reply(id, Token(http), request, http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/review", async (Guid id, ReviewRequest request, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Review(id, Token(http), request, http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/ready", async (Guid id, ReadyRequest request, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.MarkReady(id, Token(http), request, http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/welcome", async (Guid id, WelcomeRequest request, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.TryWelcome(id, Token(http), request, http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/restore", async (Guid id, ReadyRequest request, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Restore(id, Token(http), request, http.RequestAborted), http));
        app.MapPost("/api/projects/{id:guid}/publish", async (Guid id, HttpContext http, WorkflowService workflow) =>
            Respond(await workflow.Publish(id, Token(http), http.RequestAborted), http));
    }

    private static string? Token(HttpContext http) => http.Request.Cookies[SandboxEndpoints.CookieName];

    private static IResult Respond(WorkflowResult result, HttpContext http) => result.Error switch
    {
        null => Results.Ok(result.Value),
        WorkflowError.NotFound => Results.NotFound(),
        WorkflowError.SandboxUnavailable => SandboxEndpoints.Unavailable(http),
        WorkflowError.InvalidRequest => Results.Problem(statusCode: 400, title: result.Message),
        WorkflowError.Conflict => Results.Problem(statusCode: 409, title: result.Message),
        WorkflowError.OutOfScope => Results.Problem(statusCode: 422, title: result.Message),
        _ => throw new InvalidOperationException("Unknown workflow outcome.")
    };
}
