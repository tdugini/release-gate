namespace ReleaseGate.Api.Contracts;

public sealed record ControlPlaneIdentityResponse(
    string Subject,
    string DisplayName,
    IReadOnlyList<string> Roles);
