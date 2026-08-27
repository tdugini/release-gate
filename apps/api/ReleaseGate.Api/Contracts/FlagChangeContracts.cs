namespace ReleaseGate.Api.Contracts;

public sealed record FlagChangeResponse(
    Guid Id,
    string Environment,
    bool PreviousEnabled,
    int PreviousRolloutPercentage,
    bool RequestedEnabled,
    int RequestedRolloutPercentage,
    string Status,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record FlagChangeHistoryResponse(
    IReadOnlyList<FlagChangeResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
