namespace ReleaseGate.Api.Contracts;

public sealed record EvaluateFeatureFlagRequest(
    string Environment,
    string SubjectKey);

public sealed record EvaluateFeatureFlagResponse(
    string ProjectKey,
    string FlagKey,
    string Environment,
    string SubjectKey,
    bool Enabled,
    int RolloutPercentage,
    int? Bucket,
    string Reason);
