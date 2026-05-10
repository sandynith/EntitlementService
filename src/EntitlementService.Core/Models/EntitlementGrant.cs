namespace EntitlementService.Core.Models;

/// <summary>
/// Represents a resolved entitlement path found in the graph:
/// Identity -> PartyRole -> Permission -> Resource.
/// </summary>
public record EntitlementGrant(
    string EntitlementId,
    string SubjectId,
    string RoleName,
    string PermissionName,
    string ResourceId);
