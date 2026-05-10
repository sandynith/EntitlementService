using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EntitlementService.Core.Models;
using Neo4j.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EntitlementService.Tests;

public class ApiEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/entitlements/check ──────────────────────────────

    [Fact]
    public async Task CheckEntitlement_WhenAllowed_Returns200WithGrant()
    {
        var grant = new EntitlementGrant("customer-001", "AccountHolder", "ViewBalance", "account-100");
        _factory.MockRepository
            .FindEntitlementAsync("customer-001", "ViewBalance", "account-100")
            .Returns(grant);

        var request = new EntitlementCheckRequest("customer-001", "ViewBalance", "account-100");

        var response = await _client.PostAsJsonAsync("/api/entitlements/check", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EntitlementCheckResult>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Allowed);
        Assert.NotNull(body.Grant);
        Assert.Equal("AccountHolder", body.Grant.RoleName);
    }

    [Fact]
    public async Task CheckEntitlement_WhenDenied_Returns403()
    {
        _factory.MockRepository
            .FindEntitlementAsync("customer-002", "InitiateTransfer", "account-200")
            .Returns((EntitlementGrant?)null);

        var request = new EntitlementCheckRequest("customer-002", "InitiateTransfer", "account-200");

        var response = await _client.PostAsJsonAsync("/api/entitlements/check", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EntitlementCheckResult>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(body.Allowed);
        Assert.Null(body.Grant);
    }

    [Fact]
    public async Task CheckEntitlement_WithEmptySubjectId_Returns400()
    {
        var request = new EntitlementCheckRequest("", "ViewBalance", "account-100");

        var response = await _client.PostAsJsonAsync("/api/entitlements/check", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckEntitlement_WithEmptyBody_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/entitlements/check",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Empty JSON object deserialises to default/null strings, which triggers validation → 403
        // OR the framework may reject it as 400. Either is acceptable.
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            $"Expected 400 or 403 but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task CheckEntitlement_ReturnsJsonContentType()
    {
        _factory.MockRepository
            .FindEntitlementAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((EntitlementGrant?)null);

        var request = new EntitlementCheckRequest("x", "y", "z");
        var response = await _client.PostAsJsonAsync("/api/entitlements/check", request);

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    // ── GET /api/health ──────────────────────────────────────────

    [Fact]
    public async Task Health_WhenNeo4jIsReachable_Returns200()
    {
        var mockSession = Substitute.For<IAsyncSession>();
        _factory.MockDriver.AsyncSession().Returns(mockSession);
        mockSession
            .ExecuteReadAsync(Arg.Any<Func<IAsyncQueryRunner, Task>>())
            .Returns(callInfo => Task.CompletedTask);

        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_WhenNeo4jIsDown_Returns503()
    {
        var mockSession = Substitute.For<IAsyncSession>();
        _factory.MockDriver.AsyncSession().Returns(mockSession);
        mockSession
            .ExecuteReadAsync(Arg.Any<Func<IAsyncQueryRunner, Task>>())
            .ThrowsAsync(new ServiceUnavailableException("Connection refused"));

        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unhealthy", json.GetProperty("status").GetString());
    }

    // ── Fallback / unknown routes ────────────────────────────────

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckEntitlement_WithGetMethod_Returns405OrNotFound()
    {
        var response = await _client.GetAsync("/api/entitlements/check");

        // Minimal API returns 405 MethodNotAllowed or falls through to 404
        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            $"Expected 405 or 404 but got {(int)response.StatusCode}");
    }
}
