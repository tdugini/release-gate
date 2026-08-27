namespace ReleaseGate.Api.Contracts;

public sealed record CreateFeatureFlagRequest(string Name, string Key, string? Description);

public sealed record UpdateFeatureFlagRequest(string Name, string? Description);

public sealed record UpdateFlagEnvironmentRequest(bool Enabled, int RolloutPercentage);

public sealed record FeatureFlagSummaryResponse(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    bool Enabled,
    int RolloutPercentage,
    string Environment);

public sealed record FlagEnvironmentResponse(
    string Environment,
    bool Enabled,
    int RolloutPercentage,
    DateTimeOffset UpdatedAt);

public sealed record FeatureFlagDetailResponse(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    IReadOnlyList<FlagEnvironmentResponse> Environments);
