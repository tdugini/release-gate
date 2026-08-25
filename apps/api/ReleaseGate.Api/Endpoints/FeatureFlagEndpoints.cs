using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Domain;
using ReleaseGate.Api.Infrastructure;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.Endpoints;

public static class FeatureFlagEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectKey}/flags").WithTags("Feature flags");

        group.MapGet("", GetFlags);
        group.MapGet("/{flagKey}", GetFlag);
        group.MapPost("", CreateFlag);
        group.MapPost("/{flagKey}/evaluate", EvaluateFlag);
        group.MapPatch("/{flagKey}/environments/{environmentKey}", UpdateEnvironment);

        return endpoints;
    }

    private static async Task<IResult> GetFlags(
        string projectKey,
        string? environment,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var environmentKey = string.IsNullOrWhiteSpace(environment)
            ? "production"
            : environment.Trim().ToLowerInvariant();

        var projectExists = await db.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Key == projectKey, cancellationToken);

        if (!projectExists)
        {
            return Results.NotFound();
        }

        var environmentExists = await db.Environments
            .AsNoTracking()
            .AnyAsync(x => x.Project.Key == projectKey && x.Key == environmentKey, cancellationToken);

        if (!environmentExists)
        {
            return Results.BadRequest(new { message = $"Unknown environment '{environmentKey}'." });
        }

        var flags = await db.FeatureFlags
            .AsNoTracking()
            .Where(x => x.Project.Key == projectKey)
            .OrderBy(x => x.Name)
            .Select(x => new FeatureFlagSummaryResponse(
                x.Id,
                x.Name,
                x.Key,
                x.Description,
                x.Environments
                    .Where(setting => setting.Environment.Key == environmentKey)
                    .Select(setting => setting.Enabled)
                    .Single(),
                x.Environments
                    .Where(setting => setting.Environment.Key == environmentKey)
                    .Select(setting => setting.RolloutPercentage)
                    .Single(),
                environmentKey))
            .ToListAsync(cancellationToken);

        return Results.Ok(flags);
    }

    private static async Task<IResult> GetFlag(
        string projectKey,
        string flagKey,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flag = await db.FeatureFlags
            .AsNoTracking()
            .Where(x => x.Project.Key == projectKey && x.Key == flagKey)
            .Select(x => new FeatureFlagDetailResponse(
                x.Id,
                x.Name,
                x.Key,
                x.Description,
                x.Environments
                    .OrderBy(setting => setting.Environment.SortOrder)
                    .Select(setting => new FlagEnvironmentResponse(
                        setting.Environment.Key,
                        setting.Enabled,
                        setting.RolloutPercentage,
                        setting.UpdatedAt))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return flag is null ? Results.NotFound() : Results.Ok(flag);
    }

    private static async Task<IResult> CreateFlag(
        string projectKey,
        CreateFeatureFlagRequest request,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .Include(x => x.Environments)
            .SingleOrDefaultAsync(x => x.Key == projectKey, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();
        var key = request.Key.Trim().ToLowerInvariant();

        if (name.Length is < 2 or > 120)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name must be between 2 and 120 characters."]
            });
        }

        if (!KeyRules.IsValid(key))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["key"] = ["Key must be a lowercase slug between 3 and 80 characters."]
            });
        }

        if (await db.FeatureFlags.AnyAsync(
                x => x.ProjectId == project.Id && x.Key == key,
                cancellationToken))
        {
            return Results.Conflict(new { message = $"A flag with key '{key}' already exists." });
        }

        var flag = new FeatureFlag
        {
            ProjectId = project.Id,
            Name = name,
            Key = key,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        foreach (var environment in project.Environments)
        {
            flag.Environments.Add(new FeatureFlagEnvironment
            {
                EnvironmentId = environment.Id,
                Enabled = false,
                RolloutPercentage = 0
            });
        }

        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/projects/{projectKey}/flags/{flag.Key}", new
        {
            flag.Id,
            flag.Name,
            flag.Key,
            flag.Description
        });
    }

    private static async Task<IResult> EvaluateFlag(
        string projectKey,
        string flagKey,
        EvaluateFeatureFlagRequest request,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var environmentKey = request.Environment?.Trim().ToLowerInvariant() ?? string.Empty;
        var subjectKey = request.SubjectKey?.Trim() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(environmentKey))
        {
            errors["environment"] = ["Environment is required."];
        }

        if (string.IsNullOrWhiteSpace(subjectKey) || subjectKey.Length > 200)
        {
            errors["subjectKey"] = ["Subject key is required and must be at most 200 characters."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var setting = await db.FeatureFlagEnvironments
            .AsNoTracking()
            .Where(x => x.FeatureFlag.Project.Key == projectKey
                        && x.FeatureFlag.Key == flagKey
                        && x.Environment.Key == environmentKey)
            .Select(x => new
            {
                x.Enabled,
                x.RolloutPercentage
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (setting is null)
        {
            return Results.NotFound();
        }

        var decision = FeatureFlagEvaluator.Evaluate(
            projectKey,
            flagKey,
            environmentKey,
            subjectKey,
            setting.Enabled,
            setting.RolloutPercentage);

        return Results.Ok(new EvaluateFeatureFlagResponse(
            projectKey,
            flagKey,
            environmentKey,
            subjectKey,
            decision.Enabled,
            setting.RolloutPercentage,
            decision.Bucket,
            decision.Reason));
    }

    private static async Task<IResult> UpdateEnvironment(
        string projectKey,
        string flagKey,
        string environmentKey,
        UpdateFlagEnvironmentRequest request,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.RolloutPercentage is < 0 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rolloutPercentage"] = ["Rollout percentage must be between 0 and 100."]
            });
        }

        var setting = await db.FeatureFlagEnvironments
            .Include(x => x.FeatureFlag)
            .ThenInclude(x => x.Project)
            .Include(x => x.Environment)
            .SingleOrDefaultAsync(
                x => x.FeatureFlag.Project.Key == projectKey
                     && x.FeatureFlag.Key == flagKey
                     && x.Environment.Key == environmentKey,
                cancellationToken);

        if (setting is null)
        {
            return Results.NotFound();
        }

        setting.Enabled = request.Enabled;
        setting.RolloutPercentage = request.Enabled ? request.RolloutPercentage : 0;
        setting.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FlagEnvironmentResponse(
            setting.Environment.Key,
            setting.Enabled,
            setting.RolloutPercentage,
            setting.UpdatedAt));
    }
}
