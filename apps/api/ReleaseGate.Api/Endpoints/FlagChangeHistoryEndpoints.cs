using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.Endpoints;

public static class FlagChangeHistoryEndpoints
{
    public static IEndpointRouteBuilder MapFlagChangeHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/projects/{projectKey}/flags/{flagKey}/change-history",
                GetFlagChangeHistory)
            .WithTags("Feature flags")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetFlagChangeHistory(
        string projectKey,
        string flagKey,
        int page,
        int pageSize,
        string? environment,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
        var environmentKey = string.IsNullOrWhiteSpace(environment)
            ? null
            : environment.Trim().ToLowerInvariant();

        var flagExists = await db.FeatureFlags
            .AsNoTracking()
            .AnyAsync(
                x => x.Project.Key == projectKey && x.Key == flagKey,
                cancellationToken);

        if (!flagExists)
        {
            return Results.NotFound();
        }

        if (environmentKey is not null)
        {
            var environmentExists = await db.Environments
                .AsNoTracking()
                .AnyAsync(
                    x => x.Project.Key == projectKey && x.Key == environmentKey,
                    cancellationToken);

            if (!environmentExists)
            {
                return Results.BadRequest(new { message = $"Unknown environment '{environmentKey}'." });
            }
        }

        var query = db.FlagChanges
            .AsNoTracking()
            .Where(x => x.FeatureFlagEnvironment.FeatureFlag.Project.Key == projectKey
                        && x.FeatureFlagEnvironment.FeatureFlag.Key == flagKey
                        && (environmentKey == null || x.FeatureFlagEnvironment.Environment.Key == environmentKey));

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        var items = await query
            .OrderByDescending(x => x.RequestedAt)
            .ThenByDescending(x => x.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new FlagChangeResponse(
                x.Id,
                x.FeatureFlagEnvironment.Environment.Key,
                x.PreviousEnabled,
                x.PreviousRolloutPercentage,
                x.RequestedEnabled,
                x.RequestedRolloutPercentage,
                x.Status,
                x.RequestedBy,
                x.RequestedAt,
                x.ReviewedBy,
                x.ReviewedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new FlagChangeHistoryResponse(
            items,
            totalCount,
            normalizedPage,
            normalizedPageSize,
            totalPages));
    }
}
