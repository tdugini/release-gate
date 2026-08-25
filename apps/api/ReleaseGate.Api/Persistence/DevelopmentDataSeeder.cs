using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Domain;

namespace ReleaseGate.Api.Persistence;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(ReleaseGateDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var project = new Project
        {
            Name = "Silva Commerce",
            Key = "silva-commerce",
            Description = "Progressive delivery controls for storefront and checkout releases."
        };

        var development = new ProjectEnvironment
        {
            Name = "Development",
            Key = "development",
            SortOrder = 10
        };
        var staging = new ProjectEnvironment
        {
            Name = "Staging",
            Key = "staging",
            SortOrder = 20
        };
        var production = new ProjectEnvironment
        {
            Name = "Production",
            Key = "production",
            SortOrder = 30
        };

        project.Environments.AddRange([development, staging, production]);

        AddFlag(
            project,
            "New checkout",
            "new-checkout",
            "Progressive rollout of the redesigned checkout.",
            development,
            staging,
            production,
            productionRollout: 25);

        AddFlag(
            project,
            "Search v2",
            "search-v2",
            "New search indexing and ranking pipeline.",
            development,
            staging,
            production,
            productionRollout: 100);

        AddFlag(
            project,
            "Recommendations",
            "recommendations",
            "Personalized recommendation surface.",
            development,
            staging,
            production,
            productionRollout: 0);

        AddFlag(
            project,
            "New navigation",
            "new-navigation",
            "Information architecture refresh for the storefront.",
            development,
            staging,
            production,
            productionRollout: 100);

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddFlag(
        Project project,
        string name,
        string key,
        string description,
        ProjectEnvironment development,
        ProjectEnvironment staging,
        ProjectEnvironment production,
        int productionRollout)
    {
        var flag = new FeatureFlag
        {
            Name = name,
            Key = key,
            Description = description
        };

        flag.Environments.AddRange([
            new FeatureFlagEnvironment
            {
                Environment = development,
                Enabled = true,
                RolloutPercentage = 100
            },
            new FeatureFlagEnvironment
            {
                Environment = staging,
                Enabled = true,
                RolloutPercentage = 100
            },
            new FeatureFlagEnvironment
            {
                Environment = production,
                Enabled = productionRollout > 0,
                RolloutPercentage = productionRollout
            }
        ]);

        project.FeatureFlags.Add(flag);
    }
}
