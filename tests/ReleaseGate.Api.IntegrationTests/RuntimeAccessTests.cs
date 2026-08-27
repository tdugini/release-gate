using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class RuntimeAccessTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    [Fact]
    public async Task Snapshot_rejects_missing_runtime_api_key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-auth-project", cancellationToken);

        var client = factory.CreateClient();
        var response = await client.GetAsync(
            "/api/runtime/projects/runtime-auth-project/environments/development/snapshot?subjectKey=user-1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scoped_runtime_api_key_cannot_read_another_project()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-denied-project", cancellationToken);

        var client = TestClients.CreateScopedRuntime(factory);
        var response = await client.GetAsync(
            "/api/runtime/projects/runtime-denied-project/environments/development/snapshot?subjectKey=user-1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scoped_runtime_api_key_can_read_its_allowed_project()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-allowed-project", cancellationToken);

        var client = TestClients.CreateScopedRuntime(factory);
        var response = await client.GetAsync(
            "/api/runtime/projects/runtime-allowed-project/environments/development/snapshot?subjectKey=user-1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task CreateProject(string projectKey, CancellationToken cancellationToken)
    {
        var operatorClient = TestClients.CreateOperator(factory);
        var response = await operatorClient.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Runtime access project", projectKey, null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
