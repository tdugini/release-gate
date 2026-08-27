using System.Net.Http.Headers;

namespace ReleaseGate.Api.IntegrationTests;

internal static class TestClients
{
    public const string OperatorToken = "releasegate-test-operator";
    public const string ReviewerToken = "releasegate-test-reviewer";
    public const string DualRoleToken = "releasegate-test-dual-role";

    public static HttpClient CreateOperator(ReleaseGateApiFactory factory) =>
        Create(factory, OperatorToken);

    public static HttpClient CreateReviewer(ReleaseGateApiFactory factory) =>
        Create(factory, ReviewerToken);

    public static HttpClient CreateDualRole(ReleaseGateApiFactory factory) =>
        Create(factory, DualRoleToken);

    private static HttpClient Create(ReleaseGateApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
