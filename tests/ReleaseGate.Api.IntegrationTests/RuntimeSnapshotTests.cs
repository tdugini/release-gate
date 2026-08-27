using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class RuntimeSnapshotTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _controlPlaneClient = TestClients.CreateOperator(factory);
    private readonly HttpClient _runtimeClient = TestClients.CreateRuntime(factory);

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

        var evaluation = await _controlPlaneClient.PostAsJsonAsync(
            "/api/projects/runtime-snapshot-project/flags/partial-rollout/evaluate",
            new EvaluateFeatureFlagRequest("development", subjectKey),
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        var expectedPartial = await evaluation.Content.ReadFromJsonAsync<EvaluateFeatureFlagResponse>(cancellationToken);
        Assert.NotNull(expectedPartial);

        var response = await _runtimeClient.GetAsync(
            $"/api/runtime/projects/runtime-snapshot-project/environments/development/snapshot?subjectKey={subjectKey}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("ETag", out var etagValues));
        Assert.StartsWith("W/\"", Assert.Single(etagValues));

        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private);
        Assert.True(cacheControl.NoCache);

        var snapshot = await response.Content.ReadFromJsonAsync<RuntimeSnapshotResponse>(cancellationToken);

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
    public async Task Snapshot_returns_not_modified_when_evaluated_flags_have_not_changed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string projectKey = "runtime-etag-project";
        const string flagKey = "new-checkout";
        const string subjectKey = "customer-etag";
        var snapshotUrl =
            $"/api/runtime/projects/{projectKey}/environments/development/snapshot?subjectKey={subjectKey}";

        await CreateProject(projectKey, cancellationToken);
        await CreateFlag(projectKey, "New checkout", flagKey, cancellationToken);
        await UpdateDevelopmentFlag(projectKey, flagKey, true, 100, cancellationToken);

        var firstResponse = await _runtimeClient.GetAsync(snapshotUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.True(firstResponse.Headers.TryGetValues("ETag", out var firstEtagValues));
        var firstEtag = Assert.Single(firstEtagValues);

        using var unchangedRequest = new HttpRequestMessage(HttpMethod.Get, snapshotUrl);
        unchangedRequest.Headers.TryAddWithoutValidation("If-None-Match", firstEtag);

        var unchangedResponse = await _runtimeClient.SendAsync(unchangedRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, unchangedResponse.StatusCode);
        Assert.True(unchangedResponse.Headers.TryGetValues("ETag", out var unchangedEtagValues));
        Assert.Equal(firstEtag, Assert.Single(unchangedEtagValues));
        Assert.Empty(await unchangedResponse.Content.ReadAsByteArrayAsync(cancellationToken));

        await UpdateDevelopmentFlag(projectKey, flagKey, false, 0, cancellationToken);

        using var changedRequest = new HttpRequestMessage(HttpMethod.Get, snapshotUrl);
        changedRequest.Headers.TryAddWithoutValidation("If-None-Match", firstEtag);

        var changedResponse = await _runtimeClient.SendAsync(changedRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, changedResponse.StatusCode);
        Assert.True(changedResponse.Headers.TryGetValues("ETag", out var changedEtagValues));
        Assert.NotEqual(firstEtag, Assert.Single(changedEtagValues));

        var changedSnapshot = await changedResponse.Content.ReadFromJsonAsync<RuntimeSnapshotResponse>(cancellationToken);
        Assert.NotNull(changedSnapshot);
        Assert.False(Assert.Single(changedSnapshot.Flags).Enabled);
    }

    [Fact]
    public async Task Snapshot_requires_a_subject_key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-validation-project", cancellationToken);

        var response = await _runtimeClient.GetAsync(
            "/api/runtime/projects/runtime-validation-project/environments/development/snapshot",
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Snapshot_returns_not_found_for_an_unknown_environment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProject("runtime-environment-project", cancellationToken);

        var response = await _runtimeClient.GetAsync(
            "/api/runtime/projects/runtime-environment-project/environments/unknown/snapshot?subjectKey=customer-1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task CreateProject(string projectKey, CancellationToken cancellationToken)
    {
        var response = await _controlPlaneClient.PostAsJsonAsync(
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
        var response = await _controlPlaneClient.PostAsJsonAsync(
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

        var response = await _controlPlaneClient.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
