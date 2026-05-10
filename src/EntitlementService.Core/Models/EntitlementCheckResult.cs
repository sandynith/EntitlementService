namespace EntitlementService.Core.Models;

public record EntitlementCheckResult(
    bool Allowed,
    string Reason,
    EntitlementGrant? Grant);
