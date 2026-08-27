using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ControlPlaneAuthorizationTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    [Fact]
    public async Task Control_plane_requires_authentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/projects",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reviewer_cannot_create_projects()
    {
        using var client = TestClients.CreateReviewer(factory);

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Forbidden project", "forbidden-project", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operator_cannot_review_production_changes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = TestClients.CreateOperator(factory);
        var change = await CreatePendingProductionChange(
            client,
            "operator-review-project",
            cancellationToken);

        var response = await client.PostAsync(
            $"/api/projects/operator-review-project/flags/new-checkout/changes/{change.Id}/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Requester_cannot_review_their_own_production_change()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = TestClients.CreateDualRole(factory);
        var change = await CreatePendingProductionChange(
            client,
            "self-review-project",
            cancellationToken);

        var response = await client.PostAsync(
            $"/api/projects/self-review-project/flags/new-checkout/changes/{change.Id}/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Runtime_snapshot_uses_runtime_credentials_instead_of_control_plane_authentication()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var operatorClient = TestClients.CreateOperator(factory);
        await CreateProjectAndFlag(operatorClient, "runtime-boundary-project", cancellationToken);

        var update = await operatorClient.PatchAsJsonAsync(
            "/api/projects/runtime-boundary-project/flags/new-checkout/environments/development",
            new UpdateFlagEnvironmentRequest(true, 100),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        const string snapshotUrl =
            "/api/runtime/projects/runtime-boundary-project/environments/development/snapshot?subjectKey=customer-1";

        var controlPlaneResponse = await operatorClient.GetAsync(snapshotUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, controlPlaneResponse.StatusCode);

        using var runtimeClient = TestClients.CreateRuntime(factory);
        var runtimeResponse = await runtimeClient.GetAsync(snapshotUrl, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, runtimeResponse.StatusCode);
    }

    private static async Task<FlagChangeResponse> CreatePendingProductionChange(
        HttpClient client,
        string projectKey,
        CancellationToken cancellationToken)
    {
        await CreateProjectAndFlag(client, projectKey, cancellationToken);

        var update = await client.PatchAsJsonAsync(
            $"/api/projects/{projectKey}/flags/new-checkout/environments/production",
            new UpdateFlagEnvironmentRequest(true, 25),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var change = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        return Assert.IsType<FlagChangeResponse>(change);
    }

    private static async Task CreateProjectAndFlag(
        HttpClient client,
        string projectKey,
        CancellationToken cancellationToken)
    {
        var project = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Authorization project", projectKey, null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);

        var flag = await client.PostAsJsonAsync(
            $"/api/projects/{projectKey}/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, flag.StatusCode);
    }
}
