using Xunit;
using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ProjectFlowTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_project_creates_default_environments()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(
            "Silva Commerce",
            "silva-commerce",
            "Checkout release controls"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();

        Assert.NotNull(project);
        Assert.Equal(["development", "staging", "production"],
            project.Environments.Select(x => x.Key).ToArray());
    }

    [Fact]
    public async Task Flag_can_be_enabled_for_one_environment_without_affecting_others()
    {
        await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(
            "Atlas",
            "atlas-project",
            null));

        var createFlag = await _client.PostAsJsonAsync(
            "/api/projects/atlas-project/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null));

        Assert.Equal(HttpStatusCode.Created, createFlag.StatusCode);

        var update = await _client.PatchAsJsonAsync(
            "/api/projects/atlas-project/flags/new-checkout/environments/production",
            new UpdateFlagEnvironmentRequest(true, 25));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var productionFlags = await _client.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            "/api/projects/atlas-project/flags?environment=production");

        var stagingFlags = await _client.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            "/api/projects/atlas-project/flags?environment=staging");

        Assert.True(productionFlags!.Single().Enabled);
        Assert.Equal(25, productionFlags.Single().RolloutPercentage);

        Assert.False(stagingFlags!.Single().Enabled);
        Assert.Equal(0, stagingFlags.Single().RolloutPercentage);
    }
}
