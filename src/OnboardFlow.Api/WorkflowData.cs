using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class Onboarding
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ReviewTaskId { get; set; }
    public int Version { get; set; } = 1;
    public bool WelcomeServiceAvailable { get; set; } = true;
    public DateTimeOffset? WelcomePreparedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? ReviewTaskClosedAt { get; set; }
    public string? BriefJson { get; set; }
    public List<RequestItem> Items { get; set; } = [];
    public List<OutboxMessage> Messages { get; set; } = [];
    public List<ActivityEvent> Events { get; set; } = [];
    public List<WelcomeAttempt> WelcomeAttempts { get; set; } = [];
    public RegisterEntry? RegisterEntry { get; set; }
}

public sealed class WelcomeAttempt
{
    public Guid Id { get; set; }
    public Guid OnboardingId { get; set; }
    public int Number { get; set; }
    public Guid CommandId { get; set; }
    public int ExpectedVersion { get; set; }
    public int ExpectedAttempts { get; set; }
    public bool Prepared { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class RegisterEntry
{
    public Guid Id { get; set; }
    public Guid OnboardingId { get; set; }
    public required string Reference { get; set; }
    public required string BriefJson { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
}

public sealed class RequestItem
{
    public Guid Id { get; set; }
    public Guid OnboardingId { get; set; }
    public required string Kind { get; set; }
    public List<ItemRevision> Revisions { get; set; } = [];
}

public sealed class ItemRevision
{
    public Guid Id { get; set; }
    public Guid RequestItemId { get; set; }
    public int Number { get; set; }
    public required string SubmissionKey { get; set; }
    public required string AnswerJson { get; set; }
    public required string SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string? Decision { get; set; }
    public string? Comment { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid OnboardingId { get; set; }
    public required string MessageKey { get; set; }
    public required string Recipient { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset PreparedAt { get; set; }
}

public sealed class ActivityEvent
{
    public Guid Id { get; set; }
    public Guid OnboardingId { get; set; }
    public int Sequence { get; set; }
    public required string Actor { get; set; }
    public required string Action { get; set; }
    public required string Outcome { get; set; }
    public required string Details { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed partial class FoundationDb
{
    public DbSet<Onboarding> Onboardings => Set<Onboarding>();
    public DbSet<RequestItem> RequestItems => Set<RequestItem>();
    public DbSet<ItemRevision> ItemRevisions => Set<ItemRevision>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<WelcomeAttempt> WelcomeAttempts => Set<WelcomeAttempt>();
    public DbSet<RegisterEntry> RegisterEntries => Set<RegisterEntry>();

    private static void ConfigureWorkflow(ModelBuilder model)
    {
        var onboarding = model.Entity<Onboarding>();
        onboarding.HasOne<Project>().WithOne().HasForeignKey<Onboarding>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        onboarding.HasIndex(x => x.RequestId).IsUnique();
        onboarding.HasIndex(x => x.ReviewTaskId).IsUnique();
        onboarding.Property(x => x.BriefJson).HasColumnType("jsonb");
        onboarding.ToTable(t => t.HasCheckConstraint("CK_Onboarding_ReadySnapshot",
            "(\"ReadyAt\" IS NULL AND \"BriefJson\" IS NULL AND \"ReviewTaskClosedAt\" IS NULL) OR " +
            "(\"ReadyAt\" IS NOT NULL AND \"BriefJson\" IS NOT NULL AND \"ReviewTaskClosedAt\" IS NOT NULL)"));
        onboarding.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OnboardingId).OnDelete(DeleteBehavior.Cascade);
        onboarding.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.OnboardingId).OnDelete(DeleteBehavior.Cascade);
        onboarding.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OnboardingId).OnDelete(DeleteBehavior.Cascade);
        onboarding.Property(x => x.WelcomeServiceAvailable).HasDefaultValue(true);
        onboarding.HasMany(x => x.WelcomeAttempts).WithOne().HasForeignKey(x => x.OnboardingId).OnDelete(DeleteBehavior.Cascade);
        onboarding.HasOne(x => x.RegisterEntry).WithOne().HasForeignKey<RegisterEntry>(x => x.OnboardingId)
            .OnDelete(DeleteBehavior.Cascade);

        var attempt = model.Entity<WelcomeAttempt>();
        attempt.HasIndex(x => new { x.OnboardingId, x.Number }).IsUnique();
        attempt.HasIndex(x => new { x.OnboardingId, x.CommandId }).IsUnique();
        attempt.ToTable(t => t.HasCheckConstraint("CK_WelcomeAttempt_Budget", "\"Number\" BETWEEN 1 AND 3"));

        var register = model.Entity<RegisterEntry>();
        register.Property(x => x.Reference).HasMaxLength(40);
        register.Property(x => x.BriefJson).HasColumnType("jsonb");
        register.HasIndex(x => x.Reference).IsUnique();

        var item = model.Entity<RequestItem>();
        item.Property(x => x.Kind).HasMaxLength(20);
        item.HasIndex(x => new { x.OnboardingId, x.Kind }).IsUnique();
        item.ToTable(t => t.HasCheckConstraint("CK_RequestItem_Kind", "\"Kind\" IN ('goals', 'pages', 'assets')"));
        item.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.RequestItemId).OnDelete(DeleteBehavior.Cascade);

        var revision = model.Entity<ItemRevision>();
        revision.HasIndex(x => new { x.RequestItemId, x.Number }).IsUnique();
        revision.HasIndex(x => new { x.RequestItemId, x.SubmissionKey }).IsUnique();
        revision.Property(x => x.SubmissionKey).HasMaxLength(80);
        revision.Property(x => x.AnswerJson).HasColumnType("jsonb");
        revision.Property(x => x.SubmittedBy).HasMaxLength(80);
        revision.Property(x => x.Decision).HasMaxLength(20);
        revision.Property(x => x.Comment).HasMaxLength(1000);
        revision.Property(x => x.ReviewedBy).HasMaxLength(80);
        revision.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ItemRevision_Number", "\"Number\" > 0");
            t.HasCheckConstraint("CK_ItemRevision_Review",
                "(\"Decision\" IS NULL AND \"Comment\" IS NULL AND \"ReviewedBy\" IS NULL AND \"ReviewedAt\" IS NULL) OR " +
                "(\"Decision\" IS NOT NULL AND \"ReviewedBy\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL AND " +
                "((\"Decision\" = 'approved' AND \"Comment\" IS NULL) OR " +
                "(\"Decision\" = 'changes_requested' AND \"Comment\" IS NOT NULL AND length(btrim(\"Comment\")) > 0)))");
        });

        var message = model.Entity<OutboxMessage>();
        message.HasIndex(x => new { x.OnboardingId, x.MessageKey }).IsUnique();
        message.Property(x => x.MessageKey).HasMaxLength(80);
        message.Property(x => x.Recipient).HasMaxLength(80);
        message.Property(x => x.Subject).HasMaxLength(160);
        message.Property(x => x.Body).HasMaxLength(2000);

        var activity = model.Entity<ActivityEvent>();
        activity.HasIndex(x => new { x.OnboardingId, x.Sequence }).IsUnique();
        activity.Property(x => x.Actor).HasMaxLength(80);
        activity.Property(x => x.Action).HasMaxLength(120);
        activity.Property(x => x.Outcome).HasMaxLength(40);
        activity.Property(x => x.Details).HasMaxLength(1000);
    }
}

public sealed record AnswerContent(string? Goal, string? Audience, string[] Pages, string[] AssetReferences);
public sealed record RevisionView(Guid Id, int Number, AnswerContent Answer, string SubmittedBy,
    DateTimeOffset SubmittedAt, string? Decision, string? Comment, string? ReviewedBy, DateTimeOffset? ReviewedAt);
public sealed record ItemView(Guid Id, string Kind, string Label, string State, string? Validation,
    RevisionView[] Revisions);
public sealed record OnboardingView(Guid Id, Guid RequestId, Guid ReviewTaskId, int Version,
    DateTimeOffset? WelcomePreparedAt, DateTimeOffset? ReadyAt, DateTimeOffset? ReviewTaskClosedAt,
    bool WelcomeServiceAvailable, WelcomeAttemptView[] WelcomeAttempts);
public sealed record WelcomeAttemptView(Guid Id, int Number, Guid CommandId, bool Prepared, DateTimeOffset At);
public sealed record RegisterView(Guid Id, string Reference, DateTimeOffset ConfirmedAt, KickoffBrief Brief);
public sealed record MessageView(Guid Id, string Recipient, string Subject, string Body, DateTimeOffset PreparedAt);
public sealed record ActivityView(Guid Id, int Sequence, string Actor, string Action, string Outcome,
    string Details, DateTimeOffset At);
public sealed record ApprovedRevision(string Kind, Guid RevisionId, int Number, string ReviewedBy, DateTimeOffset ReviewedAt);
public sealed record KickoffBrief(string ProjectReference, string ProjectName, string ClientName,
    string ClientContact, string Owner, string Service, string Goal, string Audience, string[] Pages,
    string[] AssetReferences, ApprovedRevision[] Approvals, DateTimeOffset SavedAt);
public sealed record WorkflowView(Guid ProjectId, string State, bool CanMarkReady, OnboardingView? Onboarding,
    ItemView[] Items, MessageView[] Outbox, ActivityView[] Activity, KickoffBrief? Brief, RegisterView? RegisterEntry);
public sealed record SampleReplyRequest(int Version, string Sample, Guid? RevisionId);
public sealed record ReviewRequest(int Version, Guid RevisionId, string Decision, string? Comment);
public sealed record ReadyRequest(int Version);
public sealed record WelcomeRequest(int Version, int ExpectedAttempts, Guid CommandId);
