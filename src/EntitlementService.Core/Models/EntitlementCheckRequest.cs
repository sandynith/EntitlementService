namespace EntitlementService.Core.Models;

public record EntitlementCheckRequest(
    string SubjectId,
    string PermissionName,
    string ResourceId);
