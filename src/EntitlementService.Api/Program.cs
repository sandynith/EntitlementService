using EntitlementService.Api.Graph;
using EntitlementService.Core.Interfaces;
using EntitlementService.Core.Models;
using EntitlementService.Core.Services;
using Neo4j.Driver;

const int TIMEOUT_SECONDS = 5;

var builder = WebApplication.CreateBuilder(args);

// Load .env from solution root (for local development)
var envPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env");
if (File.Exists(envPath))
    DotNetEnv.Env.Load(envPath);

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
    GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUser, neo4jPassword),
        o => o.WithConnectionTimeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
              .WithConnectionAcquisitionTimeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))));

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
app.MapGet("/api/health", async (IDriver driver, ILogger<Program> logger) =>
{
    try
    {
        await using var session = driver.AsyncSession();
        await session.ExecuteReadAsync(async tx =>
        {
            await tx.RunAsync("RETURN 1");
        }).WaitAsync(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

        return Results.Ok(new { status = "healthy", database = "connected" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed — DB not connected.");
        return Results.Json(
            new { status = "unhealthy", database = "disconnected" },
            statusCode: 503);
    }
})
.WithName("HealthCheck")
.WithTags("Health")
.WithOpenApi();

// POST /api/entitlements/check — evaluate an entitlement
app.MapPost("/api/entitlements/check", async (
    EntitlementCheckRequest request,
    EntitlementCheckService service,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.SubjectId) ||
        string.IsNullOrWhiteSpace(request.PermissionName) ||
        string.IsNullOrWhiteSpace(request.ResourceId))
    {
        return Results.Json(
            new { allowed = false, reason = "SubjectId, PermissionName, and ResourceId are required." },
            statusCode: 400);
    }

    try
    {
        var result = await service.EvaluateAsync(request).WaitAsync(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
        return result.Allowed
            ? Results.Ok(result)
            : Results.Json(result, statusCode: 403);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Entitlement check failed — DB not connected: Subject={SubjectId} Permission={Permission} Resource={Resource}",
            request.SubjectId, request.PermissionName, request.ResourceId);
        return Results.Json(
            new { allowed = false, reason = "Service temporarily unavailable. Please try again later." },
            statusCode: 503);
    }
})
.WithName("CheckEntitlement")
.WithTags("Entitlements")
.WithOpenApi();

// POST /api/seed — dev only, loads demo data into Neo4j
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/seed", async (DemoDataSeeder seeder, ILogger<Program> logger) =>
    {
        try
        {
            await seeder.SeedAsync().WaitAsync(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
            return Results.Ok(new { message = "Demo data seeded successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed demo data. DB is not connected");
            return Results.Json(
                new { error = "Failed to seed demo data." },
                statusCode: 503);
        }
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
