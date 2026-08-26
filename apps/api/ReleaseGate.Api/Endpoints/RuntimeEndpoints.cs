using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Infrastructure;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.Endpoints;

public static class RuntimeEndpoints
{
    public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/runtime/projects/{projectKey}/environments/{environmentKey}")
            .WithTags("Runtime");

        group.MapGet("/snapshot", GetSnapshot);

        return endpoints;
    }

    private static async Task<IResult> GetSnapshot(
        string projectKey,
        string environmentKey,
        string? subjectKey,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var normalizedEnvironmentKey = environmentKey.Trim().ToLowerInvariant();
        var normalizedSubjectKey = subjectKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSubjectKey) || normalizedSubjectKey.Length > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subjectKey"] = ["Subject key is required and must be at most 200 characters."]
            });
        }

        var environmentExists = await db.Environments
            .AsNoTracking()
            .AnyAsync(
                x => x.Project.Key == projectKey && x.Key == normalizedEnvironmentKey,
                cancellationToken);

        if (!environmentExists)
        {
            return Results.NotFound();
        }

        var settings = await db.FeatureFlagEnvironments
            .AsNoTracking()
            .Where(x => x.FeatureFlag.Project.Key == projectKey
                        && x.Environment.Key == normalizedEnvironmentKey)
            .OrderBy(x => x.FeatureFlag.Key)
            .Select(x => new
            {
                x.FeatureFlag.Key,
                x.Enabled,
                x.RolloutPercentage
            })
            .ToListAsync(cancellationToken);

        var flags = settings
            .Select(setting =>
            {
                var decision = FeatureFlagEvaluator.Evaluate(
                    projectKey,
                    setting.Key,
                    normalizedEnvironmentKey,
                    normalizedSubjectKey,
                    setting.Enabled,
                    setting.RolloutPercentage);

                return new RuntimeFlagResponse(setting.Key, decision.Enabled);
            })
            .ToList();

        return Results.Ok(new RuntimeSnapshotResponse(
            projectKey,
            normalizedEnvironmentKey,
            normalizedSubjectKey,
            DateTimeOffset.UtcNow,
            flags));
    }
}
