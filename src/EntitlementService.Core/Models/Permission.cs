namespace EntitlementService.Core.Models;

/// <summary>
/// A specific permission that can be granted on a resource
/// (e.g. ViewBalance, InitiateTransfer, ApprovePayment).
/// </summary>
public record Permission(string PermissionId, string PermissionName);
