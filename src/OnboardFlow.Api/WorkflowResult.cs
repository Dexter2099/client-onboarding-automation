namespace OnboardFlow.Api;

public enum WorkflowError { InvalidRequest, Conflict, OutOfScope, NotFound, SandboxUnavailable }

public sealed record WorkflowResult(WorkflowView? Value, WorkflowError? Error = null, string? Message = null)
{
    public static WorkflowResult Ok(WorkflowView value) => new(value);
    public static WorkflowResult Invalid(string message) => new(null, WorkflowError.InvalidRequest, message);
    public static WorkflowResult Conflict(string message) => new(null, WorkflowError.Conflict, message);
    public static WorkflowResult OutOfScope(string message) => new(null, WorkflowError.OutOfScope, message);
    public static WorkflowResult NotFound() => new(null, WorkflowError.NotFound);
    public static WorkflowResult Unavailable() => new(null, WorkflowError.SandboxUnavailable);
}
