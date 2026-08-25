using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReleaseGate.Api.Persistence;

namespace ReleaseGate.Api.IntegrationTests;

public sealed class ReleaseGateApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                service => service.ServiceType == typeof(DbContextOptions<ReleaseGateDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ReleaseGateDbContext>(options =>
                options.UseInMemoryDatabase($"releasegate-tests-{Guid.NewGuid()}"));
        });
    }
}
