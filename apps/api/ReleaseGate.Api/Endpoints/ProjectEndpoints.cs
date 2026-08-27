using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Contracts;
using ReleaseGate.Api.Domain;
using ReleaseGate.Api.Infrastructure;
using ReleaseGate.Api.Persistence;
using ReleaseGate.Api.Security;

namespace ReleaseGate.Api.Endpoints;

public static class ProjectEndpoints
{
    private static readonly (string Name, string Key, int SortOrder)[] DefaultEnvironments =
    [
        ("Development", "development", 10),
        ("Staging", "staging", 20),
        ("Production", "production", 30)
    ];

    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/projects")
            .WithTags("Projects")
            .RequireAuthorization();

        group.MapGet("", GetProjects);
        group.MapGet("/{projectKey}", GetProject);
        group.MapPost("", CreateProject).RequireAuthorization(ControlPlanePolicies.Operator);

        return endpoints;
    }

    private static async Task<IResult> GetProjects(ReleaseGateDbContext db, CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProjectSummaryResponse(
                x.Id,
                x.Name,
                x.Key,
                x.Description,
                x.Environments.Count,
                x.FeatureFlags.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(projects);
    }

    private static async Task<IResult> GetProject(
        string projectKey,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Where(x => x.Key == projectKey)
            .Select(x => new ProjectDetailResponse(
                x.Id,
                x.Name,
                x.Key,
                x.Description,
                x.Environments
                    .OrderBy(environment => environment.SortOrder)
                    .Select(environment => new EnvironmentResponse(
                        environment.Id,
                        environment.Name,
                        environment.Key,
                        environment.SortOrder))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    private static async Task<IResult> CreateProject(
        CreateProjectRequest request,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
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

        if (await db.Projects.AnyAsync(x => x.Key == key, cancellationToken))
        {
            return Results.Conflict(new { message = $"A project with key '{key}' already exists." });
        }

        var project = new Project
        {
            Name = name,
            Key = key,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        foreach (var environment in DefaultEnvironments)
        {
            project.Environments.Add(new ProjectEnvironment
            {
                Name = environment.Name,
                Key = environment.Key,
                SortOrder = environment.SortOrder
            });
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/projects/{project.Key}",
            new ProjectDetailResponse(
                project.Id,
                project.Name,
                project.Key,
                project.Description,
                project.Environments
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new EnvironmentResponse(x.Id, x.Name, x.Key, x.SortOrder))
                    .ToList()));
    }
}
