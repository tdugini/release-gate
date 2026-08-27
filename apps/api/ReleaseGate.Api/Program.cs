using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Endpoints;
using ReleaseGate.Api.Persistence;
using ReleaseGate.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<ReleaseGateDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ReleaseGate")));

builder.Services.Configure<ControlPlaneAuthOptions>(
    builder.Configuration.GetSection(ControlPlaneAuthOptions.SectionName));
builder.Services.Configure<RuntimeAccessOptions>(
    builder.Configuration.GetSection(RuntimeAccessOptions.SectionName));
builder.Services.AddSingleton<RuntimeApiKeyValidator>();

builder.Services
    .AddAuthentication(ControlPlaneAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ControlPlaneAuthenticationHandler>(
        ControlPlaneAuthenticationDefaults.Scheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ControlPlanePolicies.Operator, policy =>
        policy.RequireRole(ControlPlaneRoles.Operator));
    options.AddPolicy(ControlPlanePolicies.Reviewer, policy =>
        policy.RequireRole(ControlPlaneRoles.Reviewer));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReleaseGateDbContext>();

    if (db.Database.IsRelational())
    {
        if (app.Environment.IsDevelopment())
        {
            await DatabaseMigrationBootstrapper.PrepareLegacyDatabaseAsync(db);
        }

        await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        await DevelopmentDataSeeder.SeedAsync(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("web");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "releasegate-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapAuthEndpoints();
app.MapProjectEndpoints();
app.MapFeatureFlagEndpoints();
app.MapRuntimeEndpoints();

app.Run();

public partial class Program;
