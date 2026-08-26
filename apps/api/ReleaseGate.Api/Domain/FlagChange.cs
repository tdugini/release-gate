namespace ReleaseGate.Api.Domain;

public sealed class FlagChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeatureFlagEnvironmentId { get; set; }
    public bool PreviousEnabled { get; set; }
    public int PreviousRolloutPercentage { get; set; }
    public bool RequestedEnabled { get; set; }
    public int RequestedRolloutPercentage { get; set; }
    public required string Status { get; set; }
    public required string RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public FeatureFlagEnvironment FeatureFlagEnvironment { get; set; } = null!;
}
