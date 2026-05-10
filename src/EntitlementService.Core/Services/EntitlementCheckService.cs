using EntitlementService.Core.Interfaces;
using EntitlementService.Core.Models;

namespace EntitlementService.Core.Services;

public class EntitlementCheckService
{
    private readonly IEntitlementRepository _repository;

    public EntitlementCheckService(IEntitlementRepository repository)
    {
        _repository = repository;
    }

    public async Task<EntitlementCheckResult> EvaluateAsync(EntitlementCheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SubjectId))
            return new EntitlementCheckResult(false, "SubjectId is required.", null);

        if (string.IsNullOrWhiteSpace(request.PermissionName))
            return new EntitlementCheckResult(false, "PermissionName is required.", null);

        if (string.IsNullOrWhiteSpace(request.ResourceId))
            return new EntitlementCheckResult(false, "ResourceId is required.", null);

        var grant = await _repository.FindEntitlementAsync(
            request.SubjectId,
            request.PermissionName,
            request.ResourceId);

        if (grant is not null)
        {
            return new EntitlementCheckResult(
                true,
                $"Access granted via role '{grant.RoleName}' with permission '{grant.PermissionName}'.",
                grant);
        }

        return new EntitlementCheckResult(
            false,
            $"No entitlement path found for subject '{request.SubjectId}' " +
            $"to perform '{request.PermissionName}' on resource '{request.ResourceId}'.",
            null);
    }
}
