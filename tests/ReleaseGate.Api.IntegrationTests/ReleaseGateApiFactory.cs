using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ReleaseGateApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ReleaseGateDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ReleaseGateDbContext>>();

            services.AddDbContext<ReleaseGateDbContext>(options =>
                options.UseInMemoryDatabase($"releasegate-tests-{Guid.NewGuid()}"));
        });
    }
}
