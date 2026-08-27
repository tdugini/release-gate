namespace ReleaseGate.Api.Contracts;

public sealed record CreateProjectRequest(string Name, string Key, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description);

public sealed record EnvironmentResponse(Guid Id, string Name, string Key, int SortOrder);

public sealed record ProjectSummaryResponse(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    int EnvironmentCount,
    int FlagCount);

public sealed record ProjectDetailResponse(
    Guid Id,
    string Name,
    string Key,
    string? Description,
    IReadOnlyList<EnvironmentResponse> Environments);
