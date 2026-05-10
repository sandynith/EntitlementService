using EntitlementService.Core.Interfaces;
using EntitlementService.Core.Models;
using Neo4j.Driver;

namespace EntitlementService.Api.Graph;

public class Neo4jEntitlementRepository : IEntitlementRepository
{
    private readonly IDriver _driver;

    public Neo4jEntitlementRepository(IDriver driver)
    {
        _driver = driver;
    }

    // Entitlement checks
    public async Task<EntitlementGrant?> FindEntitlementAsync(
        string subjectId, string permissionName, string resourceId)
    {
        await using var session = _driver.AsyncSession();

        const string cypher = """
            MATCH (i:Identity {subjectId: $subjectId})
                  -[:HAS_ROLE]->(role:PartyRole)
                  -[:HAS_ENTITLEMENT]->(ent:Entitlement)
                  -[:GRANTS]->(perm:Permission {name: $permissionName})
                  -[:ON_RESOURCE]->(res:Resource {resourceId: $resourceId})
            RETURN ent.entitlementId AS entitlementId,
                   role.name AS roleName,
                   perm.name AS permissionName,
                   res.resourceId AS resourceId
            LIMIT 1
            """;

        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, new
            {
                subjectId,
                permissionName,
                resourceId
            });

            if (await cursor.FetchAsync())
            {
                return new EntitlementGrant(
                    cursor.Current["entitlementId"].As<string>(),
                    subjectId,
                    cursor.Current["roleName"].As<string>(),
                    cursor.Current["permissionName"].As<string>(),
                    cursor.Current["resourceId"].As<string>());
            }

            return null;
        });

        return result;
    }
}
