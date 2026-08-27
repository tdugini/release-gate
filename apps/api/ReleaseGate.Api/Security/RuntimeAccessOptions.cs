namespace ReleaseGate.Api.Security;

public sealed class RuntimeAccessOptions
{
    public const string SectionName = "RuntimeAccess";

    public List<RuntimeApiKey> ApiKeys { get; set; } = [];
}

public sealed class RuntimeApiKey
{
    public string Key { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public List<string> Projects { get; set; } = [];
}
