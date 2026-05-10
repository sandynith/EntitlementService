using EntitlementService.Core.Interfaces;
using EntitlementService.Core.Models;
using EntitlementService.Core.Services;
using NSubstitute;

namespace EntitlementService.Tests;

public class EntitlementCheckServiceTests
{
    private readonly IEntitlementRepository _repository;
    private readonly EntitlementCheckService _service;

    public EntitlementCheckServiceTests()
    {
        _repository = Substitute.For<IEntitlementRepository>();
        _service = new EntitlementCheckService(_repository);
    }

    [Fact]
    public async Task Evaluate_WhenEntitlementPathExists_ReturnsAllowed()
    {
        var grant = new EntitlementGrant("ent-001", "customer-001", "AccountHolder", "ViewBalance", "account-100");
        _repository
            .FindEntitlementAsync("customer-001", "ViewBalance", "account-100")
            .Returns(grant);

        var request = new EntitlementCheckRequest("customer-001", "ViewBalance", "account-100");

        var result = await _service.EvaluateAsync(request);

        Assert.True(result.Allowed);
        Assert.Contains("AccountHolder", result.Reason);
        Assert.Contains("ViewBalance", result.Reason);
        Assert.NotNull(result.Grant);
        Assert.Equal("customer-001", result.Grant.SubjectId);
    }

    [Fact]
    public async Task Evaluate_WhenNoEntitlementPathExists_ReturnsDenied()
    {
        _repository
            .FindEntitlementAsync("customer-002", "InitiateTransfer", "account-200")
            .Returns((EntitlementGrant?)null);

        var request = new EntitlementCheckRequest("customer-002", "InitiateTransfer", "account-200");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Contains("No entitlement path found", result.Reason);
        Assert.Null(result.Grant);
    }

    [Fact]
    public async Task Evaluate_WhenSubjectHasNoRoles_ReturnsDenied()
    {
        _repository
            .FindEntitlementAsync("unknown-999", "ViewBalance", "account-100")
            .Returns((EntitlementGrant?)null);

        var request = new EntitlementCheckRequest("unknown-999", "ViewBalance", "account-100");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Contains("unknown-999", result.Reason);
        Assert.Null(result.Grant);
    }

    [Fact]
    public async Task Evaluate_WhenPermissionExistsButOnDifferentResource_ReturnsDenied()
    {
        _repository
            .FindEntitlementAsync("customer-001", "ViewBalance", "account-200")
            .Returns((EntitlementGrant?)null);

        var request = new EntitlementCheckRequest("customer-001", "ViewBalance", "account-200");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Contains("account-200", result.Reason);
        Assert.Null(result.Grant);
    }

    [Fact]
    public async Task Evaluate_WhenSubjectIdIsEmpty_ReturnsDeniedWithValidationMessage()
    {
        var request = new EntitlementCheckRequest("", "ViewBalance", "account-100");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Equal("SubjectId is required.", result.Reason);
    }

    [Fact]
    public async Task Evaluate_WhenPermissionNameIsEmpty_ReturnsDeniedWithValidationMessage()
    {
        var request = new EntitlementCheckRequest("customer-001", "", "account-100");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Equal("PermissionName is required.", result.Reason);
    }

    [Fact]
    public async Task Evaluate_WhenResourceIdIsEmpty_ReturnsDeniedWithValidationMessage()
    {
        var request = new EntitlementCheckRequest("customer-001", "ViewBalance", "");

        var result = await _service.EvaluateAsync(request);

        Assert.False(result.Allowed);
        Assert.Equal("ResourceId is required.", result.Reason);
    }

    [Fact]
    public async Task Evaluate_GrantedResult_ContainsCorrectGrantDetails()
    {
        var grant = new EntitlementGrant("ent-004", "customer-003", "BranchOperator", "ApprovePayment", "account-100");
        _repository
            .FindEntitlementAsync("customer-003", "ApprovePayment", "account-100")
            .Returns(grant);

        var request = new EntitlementCheckRequest("customer-003", "ApprovePayment", "account-100");

        var result = await _service.EvaluateAsync(request);

        Assert.True(result.Allowed);
        Assert.Equal("BranchOperator", result.Grant!.RoleName);
        Assert.Equal("ApprovePayment", result.Grant.PermissionName);
        Assert.Equal("account-100", result.Grant.ResourceId);
    }
}
