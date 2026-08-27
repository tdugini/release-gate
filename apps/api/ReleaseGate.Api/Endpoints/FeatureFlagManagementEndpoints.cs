using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Persistence;
using ReleaseGate.Api.Security;

namespace ReleaseGate.Api.Endpoints;

public static class FeatureFlagManagementEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/projects/{projectKey}/flags/{flagKey}")
            .WithTags("Feature flags")
            .RequireAuthorization(ControlPlanePolicies.Operator);

        group.MapPut("", UpdateFlag);
        group.MapDelete("", DeleteFlag);

        return endpoints;
    }

    private static async Task<IResult> UpdateFlag(
        string projectKey,
        string flagKey,
        UpdateFeatureFlagRequest request,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flag = await db.FeatureFlags
            .Include(x => x.Environments)
            .ThenInclude(x => x.Environment)
            .SingleOrDefaultAsync(
                x => x.Project.Key == projectKey && x.Key == flagKey,
                cancellationToken);

        if (flag is null)
        {
            return Results.NotFound();
        }

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var errors = new Dictionary<string, string[]>();

        if (name.Length is < 2 or > 120)
        {
            errors["name"] = ["Name must be between 2 and 120 characters."];
        }

        if (description?.Length > 500)
        {
            errors["description"] = ["Description must be at most 500 characters."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        flag.Name = name;
        flag.Description = description;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FeatureFlagDetailResponse(
            flag.Id,
            flag.Name,
            flag.Key,
            flag.Description,
            flag.Environments
                .OrderBy(setting => setting.Environment.SortOrder)
                .Select(setting => new FlagEnvironmentResponse(
                    setting.Environment.Key,
                    setting.Enabled,
                    setting.RolloutPercentage,
                    setting.UpdatedAt))
                .ToList()));
    }

    private static async Task<IResult> DeleteFlag(
        string projectKey,
        string flagKey,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flag = await db.FeatureFlags
            .SingleOrDefaultAsync(
                x => x.Project.Key == projectKey && x.Key == flagKey,
                cancellationToken);

        if (flag is null)
        {
            return Results.NotFound();
        }

        db.FeatureFlags.Remove(flag);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
