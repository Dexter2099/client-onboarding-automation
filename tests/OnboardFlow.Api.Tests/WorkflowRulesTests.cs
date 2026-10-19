using System.Text.Json;
using OnboardFlow.Api;

namespace OnboardFlow.Api.Tests;

[Trait("Category", "Unit")]
public sealed class WorkflowRulesTests
{
    private static readonly string[] SignedPages = ["Home", "About", "Services", "Projects", "Contact"];

    public static TheoryData<string[]> InvalidPageAnswers => new()
    {
        Array.Empty<string>(),
        new[] { "Home", "About", "Services", "Projects" },
        new[] { "Home", "About", "Services", "Projects", "Contact", "Shop" },
        new[] { "Home", "About", "Services", "Projects", "Shop" },
        new[] { "Home", "About", "Services", "Projects", "Projects" },
        new[] { "home", "About", "Services", "Projects", "Contact" }
    };

    [Theory]
    [MemberData(nameof(InvalidPageAnswers))]
    public void Pages_must_contain_every_signed_page_once_with_exact_names(string[] pages)
    {
        Assert.NotNull(WorkflowRules.Validate("pages", new(null, null, pages, []), Project()));
    }

    [Fact]
    public void Signed_pages_can_be_returned_in_a_different_order()
    {
        var answer = new AnswerContent(null, null, ["Contact", "Projects", "Services", "About", "Home"], []);

        Assert.Null(WorkflowRules.Validate("pages", answer, Project()));
    }

    [Theory]
    [InlineData(null, "Small businesses")]
    [InlineData("Explain our services", null)]
    [InlineData(" \t ", "Small businesses")]
    public void Goals_require_both_a_nonblank_goal_and_audience(string? goal, string? audience)
    {
        Assert.NotNull(WorkflowRules.Validate("goals", new(goal, audience, [], []), Project()));
    }

    [Fact]
    public void Assets_cannot_be_approved_without_a_reference()
    {
        Assert.NotNull(WorkflowRules.Validate("assets", new(null, null, [], []), Project()));
    }

    [Fact]
    public void Current_valid_approvals_and_a_prepared_welcome_allow_readiness()
    {
        Assert.True(WorkflowRules.Eligible(Project(), ApprovedOnboarding()));
    }

    [Fact]
    public void Approvals_do_not_allow_readiness_before_the_welcome_is_prepared()
    {
        var onboarding = ApprovedOnboarding();
        onboarding.WelcomePreparedAt = null;

        Assert.False(WorkflowRules.Eligible(Project(), onboarding));
    }

    [Fact]
    public void Readiness_requires_all_three_answer_categories()
    {
        var onboarding = ApprovedOnboarding();
        onboarding.Items.RemoveAt(2);

        Assert.False(WorkflowRules.Eligible(Project(), onboarding));
    }

    [Fact]
    public void A_new_reply_does_not_inherit_approval_even_when_history_is_not_ordered()
    {
        var onboarding = ApprovedOnboarding();
        var pages = onboarding.Items.Single(item => item.Kind == "pages");
        pages.Revisions.Insert(0, Revision(2, new(null, null, SignedPages, []), decision: null));

        Assert.False(WorkflowRules.Eligible(Project(), onboarding));
    }

    [Fact]
    public void Readiness_revalidates_the_latest_approved_answer_against_signed_scope()
    {
        var onboarding = ApprovedOnboarding();
        var pages = onboarding.Items.Single(item => item.Kind == "pages");
        pages.Revisions.Add(Revision(2, new(null, null, [.. SignedPages, "Shop"], [])));

        Assert.False(WorkflowRules.Eligible(Project(), onboarding));
    }

    private static Project Project() => new()
    {
        Reference = "UNIT-01",
        Name = "Fictional scope fixture",
        ClientName = "Sample studio",
        ClientContact = "Sample contact",
        Owner = "Project manager",
        Service = "Website redesign",
        SignedPages = SignedPages.ToArray()
    };

    private static Onboarding ApprovedOnboarding() => new()
    {
        WelcomePreparedAt = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
        Items =
        [
            new() { Kind = "goals", Revisions = [Revision(1, new("Explain our services", "Small businesses", [], []))] },
            new() { Kind = "pages", Revisions = [Revision(1, new(null, null, SignedPages, []))] },
            new() { Kind = "assets", Revisions = [Revision(1, new(null, null, [], ["Sample logo reference"]))] }
        ]
    };

    private static ItemRevision Revision(int number, AnswerContent answer, string? decision = "approved") => new()
    {
        Number = number,
        SubmissionKey = $"unit-{number}",
        SubmittedBy = "Sample contact",
        AnswerJson = JsonSerializer.Serialize(answer, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        Decision = decision
    };
}
