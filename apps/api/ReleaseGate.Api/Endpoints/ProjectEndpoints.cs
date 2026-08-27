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
        group.MapPut("/{projectKey}", UpdateProject).RequireAuthorization(ControlPlanePolicies.Operator);
        group.MapDelete("/{projectKey}", DeleteProject).RequireAuthorization(ControlPlanePolicies.Operator);

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
        var description = NormalizeDescription(request.Description);

        var validation = ValidateMetadata(name, description);
        if (validation is not null)
        {
            return validation;
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
            Description = description
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
            ToDetailResponse(project));
    }

    private static async Task<IResult> UpdateProject(
        string projectKey,
        UpdateProjectRequest request,
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
        var description = NormalizeDescription(request.Description);
        var validation = ValidateMetadata(name, description);
        if (validation is not null)
        {
            return validation;
        }

        project.Name = name;
        project.Description = description;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDetailResponse(project));
    }

    private static async Task<IResult> DeleteProject(
        string projectKey,
        ReleaseGateDbContext db,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .SingleOrDefaultAsync(x => x.Key == projectKey, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult? ValidateMetadata(string name, string? description)
    {
        var errors = new Dictionary<string, string[]>();

        if (name.Length is < 2 or > 120)
        {
            errors["name"] = ["Name must be between 2 and 120 characters."];
        }

        if (description?.Length > 500)
        {
            errors["description"] = ["Description must be at most 500 characters."];
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static ProjectDetailResponse ToDetailResponse(Project project) =>
        new(
            project.Id,
            project.Name,
            project.Key,
            project.Description,
            project.Environments
                .OrderBy(x => x.SortOrder)
                .Select(x => new EnvironmentResponse(x.Id, x.Name, x.Key, x.SortOrder))
                .ToList());
}
