using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Domain;
using ReleaseGate.Api.Infrastructure;
using ReleaseGate.Api.Persistence;
using ReleaseGate.Api.Security;

namespace ReleaseGate.Api.Endpoints;

public static class FeatureFlagEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/projects/{projectKey}/flags")
            .WithTags("Feature flags")
            .RequireAuthorization();

        group.MapGet("", GetFlags);
        group.MapGet("/{flagKey}", GetFlag);
        group.MapGet("/{flagKey}/changes", GetFlagChanges);
        group.MapPost("", CreateFlag).RequireAuthorization(ControlPlanePolicies.Operator);
        group.MapPost("/{flagKey}/evaluate", EvaluateFlag);
        group.MapPost("/{flagKey}/changes/{changeId:guid}/approve", ApproveFlagChange)
            .RequireAuthorization(ControlPlanePolicies.Reviewer);
        group.MapPost("/{flagKey}/changes/{changeId:guid}/reject", RejectFlagChange)
            .RequireAuthorization(ControlPlanePolicies.Reviewer);
        group.MapPatch("/{flagKey}/environments/{environmentKey}", UpdateEnvironment)
            .RequireAuthorization(ControlPlanePolicies.Operator);

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

    private static async Task<IResult> GetFlagChanges(
        string projectKey,
        string flagKey,
        string? environment,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
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

        var changes = await db.FlagChanges
            .AsNoTracking()
            .Where(x => x.FeatureFlagEnvironment.FeatureFlag.Project.Key == projectKey
                        && x.FeatureFlagEnvironment.FeatureFlag.Key == flagKey
                        && (environmentKey == null || x.FeatureFlagEnvironment.Environment.Key == environmentKey))
            .OrderByDescending(x => x.RequestedAt)
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

        return Results.Ok(changes);
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
        HttpContext httpContext,
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

        var normalizedEnvironmentKey = environmentKey.Trim().ToLowerInvariant();
        var setting = await db.FeatureFlagEnvironments
            .Include(x => x.FeatureFlag)
            .ThenInclude(x => x.Project)
            .Include(x => x.Environment)
            .SingleOrDefaultAsync(
                x => x.FeatureFlag.Project.Key == projectKey
                     && x.FeatureFlag.Key == flagKey
                     && x.Environment.Key == normalizedEnvironmentKey,
                cancellationToken);

        if (setting is null)
        {
            return Results.NotFound();
        }

        var previousEnabled = setting.Enabled;
        var previousRolloutPercentage = setting.RolloutPercentage;
        var nextRolloutPercentage = request.Enabled ? request.RolloutPercentage : 0;
        var now = DateTimeOffset.UtcNow;
        var actor = GetActor(httpContext);

        if (normalizedEnvironmentKey == "production")
        {
            var hasPendingChange = await db.FlagChanges.AnyAsync(
                x => x.FeatureFlagEnvironmentId == setting.Id
                     && x.Status == FlagChangeStatuses.Pending,
                cancellationToken);

            if (hasPendingChange)
            {
                return Results.Conflict(new
                {
                    message = "A production change is already pending review for this flag."
                });
            }

            var pendingChange = new FlagChange
            {
                FeatureFlagEnvironmentId = setting.Id,
                PreviousEnabled = previousEnabled,
                PreviousRolloutPercentage = previousRolloutPercentage,
                RequestedEnabled = request.Enabled,
                RequestedRolloutPercentage = nextRolloutPercentage,
                Status = FlagChangeStatuses.Pending,
                RequestedBy = actor,
                RequestedAt = now
            };

            db.FlagChanges.Add(pendingChange);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Accepted(value: ToResponse(pendingChange, setting.Environment.Key));
        }

        setting.Enabled = request.Enabled;
        setting.RolloutPercentage = nextRolloutPercentage;
        setting.UpdatedAt = now;

        db.FlagChanges.Add(new FlagChange
        {
            FeatureFlagEnvironmentId = setting.Id,
            PreviousEnabled = previousEnabled,
            PreviousRolloutPercentage = previousRolloutPercentage,
            RequestedEnabled = setting.Enabled,
            RequestedRolloutPercentage = setting.RolloutPercentage,
            Status = FlagChangeStatuses.Applied,
            RequestedBy = actor,
            RequestedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FlagEnvironmentResponse(
            setting.Environment.Key,
            setting.Enabled,
            setting.RolloutPercentage,
            setting.UpdatedAt));
    }

    private static Task<IResult> ApproveFlagChange(
        string projectKey,
        string flagKey,
        Guid changeId,
        HttpContext httpContext,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken) =>
        ReviewFlagChange(
            projectKey,
            flagKey,
            changeId,
            approved: true,
            httpContext,
            db,
            cancellationToken);

    private static Task<IResult> RejectFlagChange(
        string projectKey,
        string flagKey,
        Guid changeId,
        HttpContext httpContext,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken) =>
        ReviewFlagChange(
            projectKey,
            flagKey,
            changeId,
            approved: false,
            httpContext,
            db,
            cancellationToken);

    private static async Task<IResult> ReviewFlagChange(
        string projectKey,
        string flagKey,
        Guid changeId,
        bool approved,
        HttpContext httpContext,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var change = await db.FlagChanges
            .Include(x => x.FeatureFlagEnvironment)
            .ThenInclude(x => x.FeatureFlag)
            .ThenInclude(x => x.Project)
            .Include(x => x.FeatureFlagEnvironment)
            .ThenInclude(x => x.Environment)
            .SingleOrDefaultAsync(
                x => x.Id == changeId
                     && x.FeatureFlagEnvironment.FeatureFlag.Project.Key == projectKey
                     && x.FeatureFlagEnvironment.FeatureFlag.Key == flagKey,
                cancellationToken);

        if (change is null)
        {
            return Results.NotFound();
        }

        if (change.Status != FlagChangeStatuses.Pending)
        {
            return Results.Conflict(new
            {
                message = $"Change has already been {change.Status}."
            });
        }

        var actor = GetActor(httpContext);
        if (string.Equals(change.RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                message = "Production changes must be reviewed by a different user."
            });
        }

        var now = DateTimeOffset.UtcNow;
        change.Status = approved ? FlagChangeStatuses.Approved : FlagChangeStatuses.Rejected;
        change.ReviewedBy = actor;
        change.ReviewedAt = now;

        if (approved)
        {
            change.FeatureFlagEnvironment.Enabled = change.RequestedEnabled;
            change.FeatureFlagEnvironment.RolloutPercentage = change.RequestedRolloutPercentage;
            change.FeatureFlagEnvironment.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(change, change.FeatureFlagEnvironment.Environment.Key));
    }

    private static FlagChangeResponse ToResponse(FlagChange change, string environment) =>
        new(
            change.Id,
            environment,
            change.PreviousEnabled,
            change.PreviousRolloutPercentage,
            change.RequestedEnabled,
            change.RequestedRolloutPercentage,
            change.Status,
            change.RequestedBy,
            change.RequestedAt,
            change.ReviewedBy,
            change.ReviewedAt);

    private static string GetActor(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated control-plane requests require a subject claim.");
}
