using Microsoft.EntityFrameworkCore;

namespace OnboardFlow.Api;

public sealed class RevokedOwnerGrant
{
    public required string Id { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
}

public sealed class NetworkRunUsage
{
    public required string Network { get; set; }
    public DateOnly Day { get; set; }
    public int Starts { get; set; }
}

public sealed partial class FoundationDb
{
    public DbSet<RevokedOwnerGrant> RevokedOwnerGrants => Set<RevokedOwnerGrant>();
    public DbSet<NetworkRunUsage> NetworkRunUsage => Set<NetworkRunUsage>();

    private static void ConfigureDemoAbuse(ModelBuilder model)
    {
        model.Entity<RevokedOwnerGrant>().Property(x => x.Id).HasMaxLength(32);
        var usage = model.Entity<NetworkRunUsage>();
        usage.HasKey(x => new { x.Network, x.Day });
        usage.Property(x => x.Network).HasMaxLength(64);
        usage.HasIndex(x => x.Day);
        usage.ToTable(t => t.HasCheckConstraint("CK_NetworkRunUsage_Positive", "\"Starts\" > 0"));
    }
}
