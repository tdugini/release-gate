namespace ReleaseGate.Api.Contracts;

public sealed record RuntimeFlagResponse(
    string Key,
    bool Enabled);

public sealed record RuntimeSnapshotResponse(
    string ProjectKey,
    string Environment,
    string SubjectKey,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RuntimeFlagResponse> Flags);
