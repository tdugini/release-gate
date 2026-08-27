using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Infrastructure;
using ReleaseGate.Api.Persistence;
using ReleaseGate.Api.Security;

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
        HttpContext httpContext,
        ReleaseGateDbContext db,
        RuntimeApiKeyValidator runtimeApiKeyValidator,
        CancellationToken cancellationToken)
    {
        var providedApiKey = httpContext.Request.Headers[RuntimeApiKeyValidator.HeaderName].ToString();
        var credential = runtimeApiKeyValidator.FindCredential(providedApiKey);

        if (credential is null)
        {
            return Results.Unauthorized();
        }

        if (!RuntimeApiKeyValidator.CanAccessProject(credential, projectKey))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

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

        var etag = CreateSnapshotEtag(
            projectKey,
            normalizedEnvironmentKey,
            normalizedSubjectKey,
            flags);

        httpContext.Response.Headers["ETag"] = etag;
        httpContext.Response.Headers["Cache-Control"] = "private, no-cache";

        if (MatchesIfNoneMatch(httpContext.Request.Headers["If-None-Match"].ToString(), etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(new RuntimeSnapshotResponse(
            projectKey,
            normalizedEnvironmentKey,
            normalizedSubjectKey,
            DateTimeOffset.UtcNow,
            flags));
    }

    private static string CreateSnapshotEtag(
        string projectKey,
        string environmentKey,
        string subjectKey,
        IReadOnlyList<RuntimeFlagResponse> flags)
    {
        var input = new StringBuilder()
            .Append(projectKey).Append('\n')
            .Append(environmentKey).Append('\n')
            .Append(subjectKey).Append('\n');

        foreach (var flag in flags)
        {
            input.Append(flag.Key)
                .Append('=')
                .Append(flag.Enabled ? '1' : '0')
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString()));
        return $"W/\"{Convert.ToHexString(hash)}\"";
    }

    private static bool MatchesIfNoneMatch(string ifNoneMatch, string etag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        return ifNoneMatch
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal));
    }
}
