using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ReleaseGateApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"releasegate-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ControlPlaneAuth:Tokens:0:Token"] = TestClients.OperatorToken,
                ["ControlPlaneAuth:Tokens:0:Subject"] = "operator@test",
                ["ControlPlaneAuth:Tokens:0:DisplayName"] = "Test Operator",
                ["ControlPlaneAuth:Tokens:0:Roles:0"] = "operator",
                ["ControlPlaneAuth:Tokens:1:Token"] = TestClients.ReviewerToken,
                ["ControlPlaneAuth:Tokens:1:Subject"] = "reviewer@test",
                ["ControlPlaneAuth:Tokens:1:DisplayName"] = "Test Reviewer",
                ["ControlPlaneAuth:Tokens:1:Roles:0"] = "reviewer",
                ["ControlPlaneAuth:Tokens:2:Token"] = TestClients.DualRoleToken,
                ["ControlPlaneAuth:Tokens:2:Subject"] = "dual-role@test",
                ["ControlPlaneAuth:Tokens:2:DisplayName"] = "Test Dual Role",
                ["ControlPlaneAuth:Tokens:2:Roles:0"] = "operator",
                ["ControlPlaneAuth:Tokens:2:Roles:1"] = "reviewer"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ReleaseGateDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ReleaseGateDbContext>>();

            services.AddDbContext<ReleaseGateDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
