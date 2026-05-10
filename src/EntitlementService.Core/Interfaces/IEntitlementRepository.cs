using EntitlementService.Core.Models;

namespace EntitlementService.Core.Interfaces;

public interface IEntitlementRepository
{
    /// <summary>
    /// Traverses the graph to find a valid entitlement path:
    /// (Identity)-[:HAS_ROLE]->(PartyRole)-[:HAS_ENTITLEMENT]->(Entitlement)-[:GRANTS]->(Permission)-[:ON_RESOURCE]->(Resource)
    /// </summary>
    Task<EntitlementGrant?> FindEntitlementAsync(string subjectId, string permissionName, string resourceId);
}
