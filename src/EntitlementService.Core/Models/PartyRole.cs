namespace EntitlementService.Core.Models;

/// <summary>
/// BIAN Party Role — a role assumed by an identity within the organisation
/// (e.g. AccountHolder, CardHolder, BranchOperator).
/// </summary>
public record PartyRole(string RoleId, string RoleName);
