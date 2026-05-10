using EntitlementService.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;
using NSubstitute;

namespace EntitlementService.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public IEntitlementRepository MockRepository { get; } = Substitute.For<IEntitlementRepository>();
    public IDriver MockDriver { get; } = Substitute.For<IDriver>();     // Mock Neo4J DB driver

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Program.cs reads these before DI runs, so they must be set
        Environment.SetEnvironmentVariable("NEO4J_URI", "bolt://localhost:7687");
        Environment.SetEnvironmentVariable("NEO4J_USER", "neo4j");
        Environment.SetEnvironmentVariable("NEO4J_PASSWORD", "test");

        builder.ConfigureServices(services =>
        {
            // Replace IDriver (singleton registered by Program.cs)
            var driverDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDriver));
            if (driverDescriptor != null)
                services.Remove(driverDescriptor);
            services.AddSingleton(MockDriver);

            // Replace IEntitlementRepository so endpoints use the mock
            var repoDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEntitlementRepository));
            if (repoDescriptor != null)
                services.Remove(repoDescriptor);
            services.AddScoped(_ => MockRepository);
        });
    }
}
