using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class SandboxSession
{
    public Guid Id { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<Project> Projects { get; set; } = [];
}

public sealed class Project
{
    public Guid Id { get; set; }
    public Guid SandboxSessionId { get; set; }
    public required string Reference { get; set; }
    public required string Name { get; set; }
    public required string ClientName { get; set; }
    public required string ClientContact { get; set; }
    public required string Owner { get; set; }
    public required string Service { get; set; }
    public required string[] SignedPages { get; set; }
}

public sealed partial class FoundationDb(DbContextOptions<FoundationDb> options) : DbContext(options)
{
    public DbSet<SandboxSession> SandboxSessions => Set<SandboxSession>();
    public DbSet<Project> Projects => Set<Project>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var session = model.Entity<SandboxSession>();
        session.Property(x => x.TokenHash).HasMaxLength(64);
        session.HasIndex(x => x.TokenHash).IsUnique();
        session.HasIndex(x => x.ExpiresAt);
        session.ToTable(t => t.HasCheckConstraint("CK_SandboxSession_Lifetime",
            "\"ExpiresAt\" = \"CreatedAt\" + interval '24 hours'"));
        session.HasMany(x => x.Projects).WithOne().HasForeignKey(x => x.SandboxSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        var project = model.Entity<Project>();
        // The leading owner column also indexes the foreign key for reads/reset cascades.
        project.HasIndex(x => new { x.SandboxSessionId, x.Reference }).IsUnique();
        project.Property(x => x.Reference).HasMaxLength(20);
        project.Property(x => x.Name).HasMaxLength(120);
        project.Property(x => x.ClientName).HasMaxLength(80);
        project.Property(x => x.ClientContact).HasMaxLength(80);
        project.Property(x => x.Owner).HasMaxLength(80);
        project.Property(x => x.Service).HasMaxLength(80);
        ConfigureWorkflow(model);
        ConfigureIntake(model);
        ConfigureIntakeSpend(model);
        ConfigureDemoAbuse(model);
    }
}

public sealed record SessionView(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public sealed record ProjectView(Guid Id, string Reference, string Name, string ClientName,
    string ClientContact, string Owner, string Service, string[] SignedPages);
