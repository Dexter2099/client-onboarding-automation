using System.Text.Json;

namespace OnboardFlow.Api;

internal static class WorkflowRules
{
    internal static readonly string[] Kinds = ["goals", "pages", "assets"];
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static bool Eligible(Project project, Onboarding onboarding) => onboarding.WelcomePreparedAt is not null &&
        onboarding.Items.Count == Kinds.Length && onboarding.Items.All(item => Latest(item) is { Decision: "approved" } revision &&
            Validate(item.Kind, Answer(revision), project) is null);
    internal static ItemRevision? Latest(RequestItem item) => item.Revisions.MaxBy(x => x.Number);
    internal static AnswerContent Answer(ItemRevision revision) => JsonSerializer.Deserialize<AnswerContent>(revision.AnswerJson, Json)!;
    internal static string? Validate(string kind, AnswerContent answer, Project project) => kind switch
    {
        "pages" when answer.Pages.Length != project.SignedPages.Length ||
            !new HashSet<string>(answer.Pages, StringComparer.Ordinal).SetEquals(project.SignedPages) =>
            "Shop is outside the signed five-page scope. Request a correction before approving Pages.",
        "goals" when string.IsNullOrWhiteSpace(answer.Goal) || string.IsNullOrWhiteSpace(answer.Audience) => "Goals and audience are required.",
        "assets" when answer.AssetReferences.Length == 0 => "At least one sample asset reference is required.",
        _ => null
    };
    internal static AnswerContent Sample(string kind, Project project, bool corrected) => kind switch
    {
        "goals" => new("Help small creative businesses discover Juniper's services and request a consultation.",
            "Small business owners planning a brand or website project.", [], []),
        "pages" => new(null, null, corrected ? project.SignedPages.ToArray() : [.. project.SignedPages, "Shop"], []),
        _ => new(null, null, [], ["Juniper logo (sample reference)", "Brand guide v1.pdf (sample reference)",
            "Homepage photography selection (sample reference)"])
    };
    internal static string Label(string kind) => kind switch { "goals" => "Goals & audience", "pages" => "Pages", _ => "Brand assets" };
    internal static DateTimeOffset Now(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return new(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }
}
