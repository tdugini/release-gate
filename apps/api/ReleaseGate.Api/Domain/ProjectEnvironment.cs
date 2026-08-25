namespace ReleaseGate.Api.Domain;

public sealed class ProjectEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Project Project { get; set; } = null!;
    public List<FeatureFlagEnvironment> FlagSettings { get; set; } = [];
}
