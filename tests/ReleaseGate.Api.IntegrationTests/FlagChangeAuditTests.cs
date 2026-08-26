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
    public async Task Updating_a_non_production_environment_creates_an_applied_audit_entry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("audit-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/audit-project/flags/new-checkout/environments/staging",
            true,
            40,
            "integration-test");

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

    [Fact]
    public async Task Production_change_is_pending_until_it_is_approved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("approval-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/approval-project/flags/new-checkout/environments/production",
            true,
            25,
            "release-author");

        var update = await _client.SendAsync(updateRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var pendingChange = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(pendingChange);
        Assert.Equal("pending", pendingChange.Status);
        Assert.Equal("release-author", pendingChange.RequestedBy);

        var beforeApproval = await GetProductionFlag("approval-project", cancellationToken);
        Assert.False(beforeApproval.Enabled);
        Assert.Equal(0, beforeApproval.RolloutPercentage);

        using var approveRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/approval-project/flags/new-checkout/changes/{pendingChange.Id}/approve");
        approveRequest.Headers.Add("X-ReleaseGate-Actor", "release-reviewer");

        var approve = await _client.SendAsync(approveRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var approvedChange = await approve.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(approvedChange);
        Assert.Equal("approved", approvedChange.Status);
        Assert.Equal("release-reviewer", approvedChange.ReviewedBy);
        Assert.NotNull(approvedChange.ReviewedAt);

        var afterApproval = await GetProductionFlag("approval-project", cancellationToken);
        Assert.True(afterApproval.Enabled);
        Assert.Equal(25, afterApproval.RolloutPercentage);
    }

    [Fact]
    public async Task Rejecting_a_production_change_keeps_the_current_configuration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("rejection-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/rejection-project/flags/new-checkout/environments/production",
            true,
            60,
            "release-author");

        var update = await _client.SendAsync(updateRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var pendingChange = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(pendingChange);

        using var rejectRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/projects/rejection-project/flags/new-checkout/changes/{pendingChange.Id}/reject");
        rejectRequest.Headers.Add("X-ReleaseGate-Actor", "release-reviewer");

        var reject = await _client.SendAsync(rejectRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var rejectedChange = await reject.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(rejectedChange);
        Assert.Equal("rejected", rejectedChange.Status);
        Assert.Equal("release-reviewer", rejectedChange.ReviewedBy);

        var productionFlag = await GetProductionFlag("rejection-project", cancellationToken);
        Assert.False(productionFlag.Enabled);
        Assert.Equal(0, productionFlag.RolloutPercentage);
    }

    private async Task CreateProjectAndFlag(string projectKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var createProject = await _client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Audit project", projectKey, null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);

        var createFlag = await _client.PostAsJsonAsync(
            $"/api/projects/{projectKey}/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createFlag.StatusCode);
    }

    private static HttpRequestMessage CreateUpdateRequest(
        string uri,
        bool enabled,
        int rolloutPercentage,
        string actor)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(new UpdateFlagEnvironmentRequest(enabled, rolloutPercentage))
        };
        request.Headers.Add("X-ReleaseGate-Actor", actor);
        return request;
    }

    private async Task<FeatureFlagSummaryResponse> GetProductionFlag(
        string projectKey,
        CancellationToken cancellationToken)
    {
        var flags = await _client.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            $"/api/projects/{projectKey}/flags?environment=production",
            cancellationToken);

        return Assert.Single(flags!);
    }
}
