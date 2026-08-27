using System.Net.Http.Headers;
using ReleaseGate.Api.Security;

namespace ReleaseGate.Api.IntegrationTests;

internal static class TestClients
{
    public const string OperatorToken = "releasegate-test-operator";
    public const string ReviewerToken = "releasegate-test-reviewer";
    public const string DualRoleToken = "releasegate-test-dual-role";
    public const string RuntimeApiKey = "releasegate-test-runtime";
    public const string ScopedRuntimeApiKey = "releasegate-test-runtime-scoped";

    public static HttpClient CreateOperator(ReleaseGateApiFactory factory) =>
        CreateControlPlane(factory, OperatorToken);

    public static HttpClient CreateReviewer(ReleaseGateApiFactory factory) =>
        CreateControlPlane(factory, ReviewerToken);

    public static HttpClient CreateDualRole(ReleaseGateApiFactory factory) =>
        CreateControlPlane(factory, DualRoleToken);

    public static HttpClient CreateRuntime(ReleaseGateApiFactory factory) =>
        CreateRuntime(factory, RuntimeApiKey);

    public static HttpClient CreateScopedRuntime(ReleaseGateApiFactory factory) =>
        CreateRuntime(factory, ScopedRuntimeApiKey);

    private static HttpClient CreateControlPlane(ReleaseGateApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpClient CreateRuntime(ReleaseGateApiFactory factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(RuntimeApiKeyValidator.HeaderName, apiKey);
        return client;
    }
}
