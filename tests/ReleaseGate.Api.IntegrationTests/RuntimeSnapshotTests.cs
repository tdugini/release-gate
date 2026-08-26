using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class RuntimeSnapshotTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Snapshot_returns_all_flags_evaluated_for_the_subject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-snapshot-project", cancellationToken);
        await CreateFlag("runtime-snapshot-project", "Partial rollout", "partial-rollout", cancellationToken);
        await CreateFlag("runtime-snapshot-project", "Always on", "always-on", cancellationToken);

        await UpdateDevelopmentFlag(
            "runtime-snapshot-project",
            "partial-rollout",
            true,
            37,
            cancellationToken);
        await UpdateDevelopmentFlag(
            "runtime-snapshot-project",
            "always-on",
            true,
            100,
            cancellationToken);

        const string subjectKey = "customer-1042";

        var evaluation = await _client.PostAsJsonAsync(
            "/api/projects/runtime-snapshot-project/flags/partial-rollout/evaluate",
            new EvaluateFeatureFlagRequest("development", subjectKey),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        var expectedPartial = await evaluation.Content.ReadFromJsonAsync<EvaluateFeatureFlagResponse>(cancellationToken);
        Assert.NotNull(expectedPartial);

        var snapshot = await _client.GetFromJsonAsync<RuntimeSnapshotResponse>(
            $"/api/runtime/projects/runtime-snapshot-project/environments/development/snapshot?subjectKey={subjectKey}",
            cancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal("runtime-snapshot-project", snapshot.ProjectKey);
        Assert.Equal("development", snapshot.Environment);
        Assert.Equal(subjectKey, snapshot.SubjectKey);
        Assert.NotEqual(default, snapshot.GeneratedAt);
        Assert.Equal(2, snapshot.Flags.Count);

        var partial = Assert.Single(snapshot.Flags, flag => flag.Key == "partial-rollout");
        Assert.Equal(expectedPartial.Enabled, partial.Enabled);

        var alwaysOn = Assert.Single(snapshot.Flags, flag => flag.Key == "always-on");
        Assert.True(alwaysOn.Enabled);
    }

    [Fact]
    public async Task Snapshot_requires_a_subject_key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-validation-project", cancellationToken);

        var response = await _client.GetAsync(
            "/api/runtime/projects/runtime-validation-project/environments/development/snapshot",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshot_returns_not_found_for_an_unknown_environment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-environment-project", cancellationToken);

        var response = await _client.GetAsync(
            "/api/runtime/projects/runtime-environment-project/environments/unknown/snapshot?subjectKey=customer-1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task CreateProject(string projectKey, CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Runtime project", projectKey, null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task CreateFlag(
        string projectKey,
        string name,
        string flagKey,
        CancellationToken cancellationToken)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectKey}/flags",
            new CreateFeatureFlagRequest(name, flagKey, null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task UpdateDevelopmentFlag(
        string projectKey,
        string flagKey,
        bool enabled,
        int rolloutPercentage,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/projects/{projectKey}/flags/{flagKey}/environments/development")
        {
            Content = JsonContent.Create(new UpdateFlagEnvironmentRequest(enabled, rolloutPercentage))
        };
        request.Headers.Add("X-ReleaseGate-Actor", "runtime-test");

        var response = await _client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
