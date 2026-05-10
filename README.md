# Entitlement Service API

Entitlement Service API is built using .NET 8 and Neo4j graph database.
This API is BIAN compliant and checks the following:

> **Can subject X perform permission Y on resource Z?**


## BIAN:
**Identity** - A customer or user, identified by a unique `subjectId`

**Party Role** - A role the identity assumes (e.g. AccountHolder, CardHolder, BranchOperator)

**Entitlement** - A grant linking a role to a set of permissions on resources

**Permission** - A named action (e.g. ViewBalance, InitiateTransfer, ApprovePayment)

**Resource** - A protected entity (e.g. a current account, savings account, debit card)

### Architecture

The service models BIAN-aligned identity and access concepts as a directed graph:

```
(Identity) -[:HAS_ROLE]-> (PartyRole) -[:HAS_ENTITLEMENT]-> (Entitlement) -[:GRANTS]-> (Permission) -[:ON_RESOURCE]-> (Resource)
```

An entitlement check traverses this path. If a complete path exists from the subject to the requested permission on the target resource, access is **allowed**. Otherwise it is **denied**.

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker for Neo4j

### 1. Start Neo4j

```bash
docker compose up -d
```

Neo4j will be available at:
- Browser UI: http://localhost:7474
- Bolt endpoint: bolt://localhost:7687
- Credentials: `neo4j` / `entitlements`

### 2. Run the API

```bash
cd EntitlementService
dotnet build
dotnet run --project src/EntitlementService.Api
```

Swagger UI: http://localhost:5062/swagger

**API endpoints:**

GET http://localhost:5062/health

POST http://localhost:5062/api/entitlements/seed

POST http://localhost:5062/api/entitlements/check


### 3. Seed Demo Data

```bash
curl -X POST http://localhost:5062/api/entitlements/seed
```

In Dev env it creates a sample graph with 3 identities, 3 roles, 5 permissions, and 3 resources.

### 4. Entitlement Check API call

```bash
# Request
curl -X POST http://localhost:5062/api/entitlements/check \
  -H "Content-Type: application/json" \
  -d '{"subjectId":"customer-001","permissionName":"ViewBalance","resourceId":"account-100"}'

# Response: Allowed (200 OK):
{
  "allowed": true,
  "reason": "Access granted via role 'AccountHolder' with permission 'ViewBalance'.",
  "grant": {
    "subjectId": "customer-001",
    "roleName": "AccountHolder",
    "permissionName": "ViewBalance",
    "resourceId": "account-100"
  }
}
```

```bash
# Request
curl -X POST http://localhost:5062/api/entitlements/check \
  -H "Content-Type: application/json" \
  -d '{"subjectId":"customer-002","permissionName":"InitiateTransfer","resourceId":"account-200"}'

# Response - Denied (403 Forbidden)
{
  "allowed": false,
  "reason": "No entitlement path found for subject 'customer-002' to perform 'InitiateTransfer' on resource 'account-200'.",
  "grant": null
}
```

## Run Tests

```bash
dotnet test
```
### Tear down

```bash
# Remove Neo4j docker container
docker stop entitlements-neo4j-1 && docker rm entitlements-neo4j-1
```
