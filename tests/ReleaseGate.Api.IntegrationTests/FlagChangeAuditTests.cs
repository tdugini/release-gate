using System.Net;
using System.Net.Http.Json;
using ReleaseGate.Api.Contracts;
using Xunit;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class FlagChangeAuditTests(ReleaseGateApiFactory factory)
    : IClassFixture<ReleaseGateApiFactory>
{
    private readonly HttpClient _operatorClient = TestClients.CreateOperator(factory);
    private readonly HttpClient _reviewerClient = TestClients.CreateReviewer(factory);

    [Fact]
    public async Task Updating_a_non_production_environment_creates_an_applied_audit_entry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("audit-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/audit-project/flags/new-checkout/environments/staging",
            true,
            40);

        var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var changes = await _operatorClient.GetFromJsonAsync<List<FlagChangeResponse>>(
            "/api/projects/audit-project/flags/new-checkout/changes?environment=staging",
            cancellationToken);

        var change = Assert.Single(changes!);

        Assert.Equal("staging", change.Environment);
        Assert.False(change.PreviousEnabled);
        Assert.Equal(0, change.PreviousRolloutPercentage);
        Assert.True(change.RequestedEnabled);
        Assert.Equal(40, change.RequestedRolloutPercentage);
        Assert.Equal("applied", change.Status);
        Assert.Equal("operator@test", change.RequestedBy);
        Assert.Null(change.ReviewedBy);
        Assert.Null(change.ReviewedAt);
    }

    [Fact]
    public async Task Change_history_returns_requested_page_and_pagination_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("history-pagination-project");

        for (var rolloutPercentage = 1; rolloutPercentage <= 12; rolloutPercentage++)
        {
            using var updateRequest = CreateUpdateRequest(
                "/api/projects/history-pagination-project/flags/new-checkout/environments/staging",
                true,
                rolloutPercentage);

            var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }

        var firstPage = await _operatorClient.GetFromJsonAsync<FlagChangeHistoryResponse>(
            "/api/projects/history-pagination-project/flags/new-checkout/change-history?page=1&pageSize=10",
            cancellationToken);

        Assert.NotNull(firstPage);
        Assert.Equal(12, firstPage.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(10, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(10, firstPage.Items.Count);

        var secondPage = await _operatorClient.GetFromJsonAsync<FlagChangeHistoryResponse>(
            "/api/projects/history-pagination-project/flags/new-checkout/change-history?page=2&pageSize=10",
            cancellationToken);

        Assert.NotNull(secondPage);
        Assert.Equal(12, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(10, secondPage.PageSize);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Equal(2, secondPage.Items.Count);

        var firstPageIds = firstPage.Items.Select(item => item.Id).ToHashSet();
        Assert.All(secondPage.Items, item => Assert.DoesNotContain(item.Id, firstPageIds));
    }

    [Fact]
    public async Task Change_history_normalizes_invalid_page_values()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("history-normalization-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/history-normalization-project/flags/new-checkout/environments/staging",
            true,
            35);

        var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var history = await _operatorClient.GetFromJsonAsync<FlagChangeHistoryResponse>(
            "/api/projects/history-normalization-project/flags/new-checkout/change-history?page=0&pageSize=0",
            cancellationToken);

        Assert.NotNull(history);
        Assert.Equal(1, history.Page);
        Assert.Equal(10, history.PageSize);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(1, history.TotalPages);
        Assert.Single(history.Items);
    }

    [Fact]
    public async Task Production_change_is_pending_until_it_is_approved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("approval-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/approval-project/flags/new-checkout/environments/production",
            true,
            25);

        var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var pendingChange = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(pendingChange);
        Assert.Equal("pending", pendingChange.Status);
        Assert.Equal("operator@test", pendingChange.RequestedBy);

        var beforeApproval = await GetProductionFlag("approval-project", cancellationToken);
        Assert.False(beforeApproval.Enabled);
        Assert.Equal(0, beforeApproval.RolloutPercentage);

        var approve = await _reviewerClient.PostAsync(
            $"/api/projects/approval-project/flags/new-checkout/changes/{pendingChange.Id}/approve",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var approvedChange = await approve.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(approvedChange);
        Assert.Equal("approved", approvedChange.Status);
        Assert.Equal("reviewer@test", approvedChange.ReviewedBy);
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
            60);

        var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var pendingChange = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(pendingChange);

        var reject = await _reviewerClient.PostAsync(
            $"/api/projects/rejection-project/flags/new-checkout/changes/{pendingChange.Id}/reject",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var rejectedChange = await reject.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(rejectedChange);
        Assert.Equal("rejected", rejectedChange.Status);
        Assert.Equal("reviewer@test", rejectedChange.ReviewedBy);

        var productionFlag = await GetProductionFlag("rejection-project", cancellationToken);
        Assert.False(productionFlag.Enabled);
        Assert.Equal(0, productionFlag.RolloutPercentage);
    }

    [Fact]
    public async Task Second_production_change_is_rejected_while_one_is_pending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("pending-conflict-project");

        using var firstRequest = CreateUpdateRequest(
            "/api/projects/pending-conflict-project/flags/new-checkout/environments/production",
            true,
            30);

        var firstUpdate = await _operatorClient.SendAsync(firstRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, firstUpdate.StatusCode);

        using var secondRequest = CreateUpdateRequest(
            "/api/projects/pending-conflict-project/flags/new-checkout/environments/production",
            true,
            80);

        var secondUpdate = await _operatorClient.SendAsync(secondRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondUpdate.StatusCode);

        var changes = await _operatorClient.GetFromJsonAsync<List<FlagChangeResponse>>(
            "/api/projects/pending-conflict-project/flags/new-checkout/changes?environment=production",
            cancellationToken);

        var pendingChange = Assert.Single(changes!);
        Assert.Equal("pending", pendingChange.Status);
        Assert.Equal(30, pendingChange.RequestedRolloutPercentage);

        var productionFlag = await GetProductionFlag("pending-conflict-project", cancellationToken);
        Assert.False(productionFlag.Enabled);
        Assert.Equal(0, productionFlag.RolloutPercentage);
    }

    [Fact]
    public async Task Reviewing_an_already_reviewed_production_change_returns_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateProjectAndFlag("review-conflict-project");

        using var updateRequest = CreateUpdateRequest(
            "/api/projects/review-conflict-project/flags/new-checkout/environments/production",
            true,
            45);

        var update = await _operatorClient.SendAsync(updateRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, update.StatusCode);

        var pendingChange = await update.Content.ReadFromJsonAsync<FlagChangeResponse>(cancellationToken);
        Assert.NotNull(pendingChange);

        var approve = await _reviewerClient.PostAsync(
            $"/api/projects/review-conflict-project/flags/new-checkout/changes/{pendingChange.Id}/approve",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var reject = await _reviewerClient.PostAsync(
            $"/api/projects/review-conflict-project/flags/new-checkout/changes/{pendingChange.Id}/reject",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);

        var productionFlag = await GetProductionFlag("review-conflict-project", cancellationToken);
        Assert.True(productionFlag.Enabled);
        Assert.Equal(45, productionFlag.RolloutPercentage);

        var changes = await _operatorClient.GetFromJsonAsync<List<FlagChangeResponse>>(
            "/api/projects/review-conflict-project/flags/new-checkout/changes?environment=production",
            cancellationToken);

        var reviewedChange = Assert.Single(changes!);
        Assert.Equal("approved", reviewedChange.Status);
        Assert.Equal("reviewer@test", reviewedChange.ReviewedBy);
    }

    private async Task CreateProjectAndFlag(string projectKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var createProject = await _operatorClient.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest("Audit project", projectKey, null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createProject.StatusCode);

        var createFlag = await _operatorClient.PostAsJsonAsync(
            $"/api/projects/{projectKey}/flags",
            new CreateFeatureFlagRequest("New checkout", "new-checkout", null),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createFlag.StatusCode);
    }

    private static HttpRequestMessage CreateUpdateRequest(
        string uri,
        bool enabled,
        int rolloutPercentage) =>
        new(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(new UpdateFlagEnvironmentRequest(enabled, rolloutPercentage))
        };

    private async Task<FeatureFlagSummaryResponse> GetProductionFlag(
        string projectKey,
        CancellationToken cancellationToken)
    {
        var flags = await _operatorClient.GetFromJsonAsync<List<FeatureFlagSummaryResponse>>(
            $"/api/projects/{projectKey}/flags?environment=production",
            cancellationToken);

        return Assert.Single(flags!);
    }
}
