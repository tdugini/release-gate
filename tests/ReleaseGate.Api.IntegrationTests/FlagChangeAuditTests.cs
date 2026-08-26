using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class FlagChangeAuditTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Updating_an_environment_creates_an_audit_entry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var createProject = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Audit project", "audit-project", null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);

        var createFlag = await _client.PostAsJsonAsync(
            "/api/projects/audit-project/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createFlag.StatusCode);

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            "/api/projects/audit-project/flags/new-checkout/environments/staging")
        {
            Content = JsonContent.Create(new UpdateFlagEnvironmentRequest(true, 40))
        };
        updateRequest.Headers.Add("X-ReleaseGate-Actor", "integration-test");

        var update = await _client.SendAsync(updateRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var changes = await _client.GetFromJsonAsync<List<FlagChangeResponse>>(
            "/api/projects/audit-project/flags/new-checkout/changes?environment=staging",
            cancellationToken);

        var change = Assert.Single(changes!);

        Assert.Equal("staging", change.Environment);
        Assert.False(change.PreviousEnabled);
        Assert.Equal(0, change.PreviousRolloutPercentage);
        Assert.True(change.RequestedEnabled);
        Assert.Equal(40, change.RequestedRolloutPercentage);
        Assert.Equal("applied", change.Status);
        Assert.Equal("integration-test", change.RequestedBy);
        Assert.Null(change.ReviewedBy);
        Assert.Null(change.ReviewedAt);
    }
}
