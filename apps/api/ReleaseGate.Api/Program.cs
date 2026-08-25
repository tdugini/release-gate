using Microsoft.EntityFrameworkCore;
using ReleaseGate.Api.Endpoints;
using ReleaseGate.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<ReleaseGateDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ReleaseGate")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("web");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ReleaseGateDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DevelopmentDataSeeder.SeedAsync(db);
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "releasegate-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapProjectEndpoints();
app.MapFeatureFlagEndpoints();

app.Run();

public partial class Program;
