using EntitlementService.Api.Graph;
using EntitlementService.Core.Interfaces;
using EntitlementService.Core.Models;
using EntitlementService.Core.Services;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if present (for local development)
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Entitlement Service API",
        Version = "v1",
        Description = "BIAN-aligned graph-backed entitlement check service"
    });
});

// Neo4j driver (singleton — thread-safe, manages its own connection pool)
var neo4jUri = Environment.GetEnvironmentVariable("NEO4J_URI");
var neo4jUser = Environment.GetEnvironmentVariable("NEO4J_USER");
var neo4jPassword = Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

if (string.IsNullOrEmpty(neo4jUri) || string.IsNullOrEmpty(neo4jUser) || string.IsNullOrEmpty(neo4jPassword))
    throw new InvalidOperationException("Neo4j configuration is not defined. Set NEO4J_URI, NEO4J_USER, and NEO4J_PASSWORD environment variables.");

builder.Services.AddSingleton<IDriver>(_ =>
    GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUser, neo4jPassword)));

builder.Services.AddScoped<IEntitlementRepository, Neo4jEntitlementRepository>();
builder.Services.AddScoped<EntitlementCheckService>();
builder.Services.AddScoped<DemoDataSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// GET api/health — checks API and Neo4j connectivity
app.MapGet("/api/health", async (IDriver driver) =>
{
    try
    {
        await using var session = driver.AsyncSession();
        await session.ExecuteReadAsync(async tx =>
        {
            await tx.RunAsync("RETURN 1");
        });

        return Results.Ok(new { status = "healthy", neo4j = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", neo4j = "disconnected", error = ex.Message },
            statusCode: 503);
    }
})
.WithName("HealthCheck")
.WithTags("Health")
.WithOpenApi();

// POST /api/entitlements/check — evaluate an entitlement
app.MapPost("/api/entitlements/check", async (
    EntitlementCheckRequest request,
    EntitlementCheckService service) =>
{
    var result = await service.EvaluateAsync(request);
    return result.Allowed
        ? Results.Ok(result)
        : Results.Json(result, statusCode: 403);
})
.WithName("CheckEntitlement")
.WithTags("Entitlements")
.WithOpenApi();

// POST /api/seed — dev only, loads demo data into Neo4j
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/seed", async (DemoDataSeeder seeder) =>
    {
        await seeder.SeedAsync();
        return Results.Ok(new { message = "Demo data seeded successfully." });
    })
    .WithName("SeedData")
    .WithTags("Admin")
    .WithOpenApi();
}

// Catch-all for unknown routes
app.MapFallback(() => Results.NotFound(new { error = "Resource not found." }));

app.Run();

// Required for integration test access
public partial class Program { }
