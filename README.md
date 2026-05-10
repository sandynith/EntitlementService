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

Swagger UI: http://localhost:5021/swagger

**API endpoints:**

GET http://localhost:5021/api/health

POST http://localhost:5021/api/seed

POST http://localhost:5021/api/entitlements/check


### 3. Seed Demo Data

```bash
curl -X POST http://localhost:5021/api/seed
```

In Dev env it creates a sample graph with 3 identities, 3 roles, 5 permissions, and 3 resources.

### 4. Entitlement Check API call

```bash
# Request
curl -X POST http://localhost:5021/api/entitlements/check \
  -H "Content-Type: application/json" \
  -d '{"subjectId":"customer-001","permissionName":"ViewBalance","resourceId":"account-100"}'

# Response: Allowed (200 OK):
{
  "allowed": true,
  "reason": "Access granted via role 'AccountHolder' with permission 'ViewBalance'.",
  "grant": {
    "entitlementId": "ent-003",
    "subjectId": "customer-001",
    "roleName": "AccountHolder",
    "permissionName": "ViewBalance",
    "resourceId": "account-100"
  }
}
```

```bash
# Request
curl -X POST http://localhost:5021/api/entitlements/check \
  -H "Content-Type: application/json" \
  -d '{"subjectId":"customer-002","permissionName":"InitiateTransfer","resourceId":"account-200"}'

# Response - Denied (403 Forbidden)
{
  "allowed": false,
  "reason": "No entitlement path found for subject 'customer-002' to perform 'InitiateTransfer' on resource 'account-200'.",
  "grant": null
}
```

## How to test

1. Run unit tests - it runs unit test and API tests with mocked responses
```bash
dotnet test
```

2. E2E test: Seed Neo4j DB and then test using `EntitlementService.Api.http`


### Tear down

```bash
# Remove Neo4j docker container
docker stop entitlements-neo4j-1 && docker rm entitlements-neo4j-1

# Free up port if its in use
lsof -ti:5021
kill <pid>
```

### Demo Seeder Data

**Entitlement Mappings:**

| Identity | Role | Permissions | Resources |
|----------|------|-------------|-----------|
| customer-001 | AccountHolder | ViewBalance, InitiateTransfer, ViewStatement | account-100 |
| customer-001 | CardHolder | BlockCard | card-300 |
| customer-002 | AccountHolder | ViewBalance | account-200 |
| operator-001 | BranchOperator | ViewUserProfile | account-100, account-200, card-300 |

**Ungranted:** `ApprovePayment` exists as a permission but is not assigned to anyone.
