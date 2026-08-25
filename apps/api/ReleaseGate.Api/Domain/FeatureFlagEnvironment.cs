namespace ReleaseGate.Api.Domain;

public sealed class FeatureFlagEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeatureFlagId { get; set; }
    public Guid EnvironmentId { get; set; }
    public bool Enabled { get; set; }
    public int RolloutPercentage { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public FeatureFlag FeatureFlag { get; set; } = null!;
    public ProjectEnvironment Environment { get; set; } = null!;
}
