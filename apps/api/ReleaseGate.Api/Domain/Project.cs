namespace ReleaseGate.Api.Domain;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Key { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ProjectEnvironment> Environments { get; set; } = [];
    public List<FeatureFlag> FeatureFlags { get; set; } = [];
}
