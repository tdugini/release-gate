namespace ReleaseGate.Api.Security;

public static class ControlPlaneAuthenticationDefaults
{
    public const string Scheme = "ReleaseGateControlPlane";
}

public static class ControlPlaneRoles
{
    public const string Operator = "operator";
    public const string Reviewer = "reviewer";
}

public static class ControlPlanePolicies
{
    public const string Operator = "control-plane-operator";
    public const string Reviewer = "control-plane-reviewer";
}

public sealed class ControlPlaneAuthOptions
{
    public const string SectionName = "ControlPlaneAuth";

    public List<ControlPlanePrincipalOptions> Tokens { get; init; } = [];
}

public sealed class ControlPlanePrincipalOptions
{
    public string Token { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public List<string> Roles { get; init; } = [];
}
