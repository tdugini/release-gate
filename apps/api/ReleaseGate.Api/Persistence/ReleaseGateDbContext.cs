using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Domain;

namespace ReleaseGate.Api.Persistence;

public sealed class ReleaseGateDbContext(DbContextOptions<ReleaseGateDbContext> options)
    : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFlagEnvironment> FeatureFlagEnvironments => Set<FeatureFlagEnvironment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<ProjectEnvironment>(entity =>
        {
            entity.ToTable("environments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
            entity.HasOne(x => x.Project)
                .WithMany(x => x.Environments)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.ToTable("feature_flags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
            entity.HasOne(x => x.Project)
                .WithMany(x => x.FeatureFlags)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeatureFlagEnvironment>(entity =>
        {
            entity.ToTable("feature_flag_environments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FeatureFlagId, x.EnvironmentId }).IsUnique();
            entity.HasOne(x => x.FeatureFlag)
                .WithMany(x => x.Environments)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Environment)
                .WithMany(x => x.FlagSettings)
                .HasForeignKey(x => x.EnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
