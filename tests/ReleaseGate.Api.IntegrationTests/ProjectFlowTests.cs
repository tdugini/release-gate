using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ProjectFlowTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_project_creates_default_environments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest(
                "Silva Commerce",
                "silva-commerce",
                "Checkout release controls"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>(cancellationToken);

        Assert.NotNull(project);
        Assert.Equal(
            ["development", "staging", "production"],
            project.Environments.Select(x => x.Key).ToArray());
    }

    [Fact]
    public async Task Flag_can_be_enabled_for_one_environment_without_affecting_others()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var createProject = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Atlas", "atlas-project", null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);

        var createFlag = await _client.PostAsJsonAsync(
            "/api/projects/atlas-project/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createFlag.StatusCode);

        var update = await _client.PatchAsJsonAsync(
            "/api/projects/atlas-project/flags/new-checkout/environments/staging",
            new UpdateFlagEnvironmentRequest(true, 25),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var productionFlags = await _client.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            "/api/projects/atlas-project/flags?environment=production",
            cancellationToken);

        var stagingFlags = await _client.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            "/api/projects/atlas-project/flags?environment=staging",
            cancellationToken);

        Assert.NotNull(productionFlags);
        Assert.NotNull(stagingFlags);

        var productionFlag = Assert.Single(productionFlags);
        var stagingFlag = Assert.Single(stagingFlags);

        Assert.True(stagingFlag.Enabled);
        Assert.Equal(25, stagingFlag.RolloutPercentage);

        Assert.False(productionFlag.Enabled);
        Assert.Equal(0, productionFlag.RolloutPercentage);
    }
}
