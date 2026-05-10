using Neo4j.Driver;

namespace EntitlementService.Api.Graph;

/// <summary>
/// Seeds Neo4j with demo data in development only.
/// Not intended for production deployment.
/// </summary>
public class DemoDataSeeder
{
    private readonly IDriver _driver;

    public DemoDataSeeder(IDriver driver)
    {
        _driver = driver;
    }

    public async Task SeedAsync()
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Clear existing data
            await tx.RunAsync("MATCH (n) DETACH DELETE n");

            // Create Identities (customers)
            await tx.RunAsync("""
                CREATE (i1:Identity {subjectId: 'customer-001', name: 'Sandeep Sharma'})
                CREATE (i2:Identity {subjectId: 'customer-002', name: 'Elon Musk'})
                CREATE (i3:Identity {subjectId: 'customer-003', name: 'Tom Cruise'})
                CREATE (i4:Identity {subjectId: 'customer-004', name: 'Poor Charlie'})
                """);

            // Create Party Roles (BIAN-aligned)
            await tx.RunAsync("""
                CREATE (r1:PartyRole {roleId: 'role-account-holder', name: 'AccountHolder'})
                CREATE (r2:PartyRole {roleId: 'role-card-holder', name: 'CardHolder'})
                CREATE (r3:PartyRole {roleId: 'role-branch-operator', name: 'BranchOperator'})
                """);

            // Create Permissions
            await tx.RunAsync("""
                CREATE (p1:Permission {permissionId: 'perm-view-balance', name: 'ViewBalance'})
                CREATE (p2:Permission {permissionId: 'perm-initiate-transfer', name: 'InitiateTransfer'})
                CREATE (p3:Permission {permissionId: 'perm-approve-payment', name: 'ApprovePayment'})
                CREATE (p4:Permission {permissionId: 'perm-view-statement', name: 'ViewStatement'})
                CREATE (p5:Permission {permissionId: 'perm-block-card', name: 'BlockCard'})
                CREATE (p6:Permission {permissionId: 'perm-view-user-profile', name: 'ViewUserProfile'})
                """);

            // Create Resources
            await tx.RunAsync("""
                CREATE (res1:Resource {resourceId: 'account-100', resourceType: 'CurrentAccount'})
                CREATE (res2:Resource {resourceId: 'account-200', resourceType: 'SavingsAccount'})
                CREATE (res3:Resource {resourceId: 'card-300', resourceType: 'DebitCard'})
                """);

            // Wire up: Sandeep is an AccountHolder with ViewBalance + InitiateTransfer on account-100
            await tx.RunAsync("""
                MATCH (i:Identity {subjectId: 'customer-001'})
                MATCH (role:PartyRole {name: 'AccountHolder'})
                CREATE (i)-[:HAS_ROLE]->(role)

                WITH role
                CREATE (ent:Entitlement {entitlementId: 'ent-001', description: 'Account holder standard entitlements'})
                CREATE (role)-[:HAS_ENTITLEMENT]->(ent)

                WITH ent
                MATCH (p1:Permission {name: 'ViewBalance'})
                MATCH (p2:Permission {name: 'InitiateTransfer'})
                MATCH (p3:Permission {name: 'ViewStatement'})
                MATCH (res:Resource {resourceId: 'account-100'})
                CREATE (ent)-[:GRANTS]->(p1)-[:ON_RESOURCE]->(res)
                CREATE (ent)-[:GRANTS]->(p2)-[:ON_RESOURCE]->(res)
                CREATE (ent)-[:GRANTS]->(p3)-[:ON_RESOURCE]->(res)
                """);

            // Wire up: Sandeep is also a CardHolder on card-300
            await tx.RunAsync("""
                MATCH (i:Identity {subjectId: 'customer-001'})
                MATCH (role:PartyRole {name: 'CardHolder'})
                CREATE (i)-[:HAS_ROLE]->(role)

                WITH role
                CREATE (ent:Entitlement {entitlementId: 'ent-002', description: 'Card holder entitlements'})
                CREATE (role)-[:HAS_ENTITLEMENT]->(ent)

                WITH ent
                MATCH (p:Permission {name: 'BlockCard'})
                MATCH (res:Resource {resourceId: 'card-300'})
                CREATE (ent)-[:GRANTS]->(p)-[:ON_RESOURCE]->(res)
                """);

            // Wire up: Elon Musk is an AccountHolder with ViewBalance only on account-200
            await tx.RunAsync("""
                MATCH (i:Identity {subjectId: 'customer-002'})
                MATCH (role:PartyRole {name: 'AccountHolder'})
                CREATE (i)-[:HAS_ROLE]->(role)

                WITH role
                CREATE (ent:Entitlement {entitlementId: 'ent-003', description: 'Savings account view only'})
                CREATE (role)-[:HAS_ENTITLEMENT]->(ent)

                WITH ent
                MATCH (p:Permission {name: 'ViewBalance'})
                MATCH (res:Resource {resourceId: 'account-200'})
                CREATE (ent)-[:GRANTS]->(p)-[:ON_RESOURCE]->(res)
                """);

            // Wire up: Tom is a BranchOperator with ApprovePayment + ViewUserProfile on account-100
            await tx.RunAsync("""
                MATCH (i:Identity {subjectId: 'customer-003'})
                MATCH (role:PartyRole {name: 'BranchOperator'})
                CREATE (i)-[:HAS_ROLE]->(role)

                WITH role
                CREATE (ent:Entitlement {entitlementId: 'ent-004', description: 'Branch operator entitlements'})
                CREATE (role)-[:HAS_ENTITLEMENT]->(ent)

                WITH ent
                MATCH (p1:Permission {name: 'ApprovePayment'})
                MATCH (p2:Permission {name: 'ViewUserProfile'})
                MATCH (res:Resource {resourceId: 'account-100'})
                CREATE (ent)-[:GRANTS]->(p1)-[:ON_RESOURCE]->(res)
                CREATE (ent)-[:GRANTS]->(p2)-[:ON_RESOURCE]->(res)
                """);
        });
    }
}
