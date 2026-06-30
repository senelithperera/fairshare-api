# FairShare API

A group expense-splitting backend built with **ASP.NET Core 8** and **Clean Architecture**. Split bills fairly among friends — with or without creating an account.

---

## Table of Contents

- [FairShare API](#fairshare-api)
  - [Table of Contents](#table-of-contents)
  - [Overview](#overview)
  - [Tech Stack](#tech-stack)
  - [Solution Structure](#solution-structure)
  - [Prerequisites](#prerequisites)
  - [Getting Started](#getting-started)
    - [1. Clone the repository](#1-clone-the-repository)
    - [2. Configure environment variables](#2-configure-environment-variables)
    - [3. Start the database](#3-start-the-database)
    - [4. Apply database migrations](#4-apply-database-migrations)
    - [5. Run the API](#5-run-the-api)
    - [6. Open the interactive docs](#6-open-the-interactive-docs)
  - [Configuration](#configuration)
    - [Useful Docker commands](#useful-docker-commands)
  - [API Reference](#api-reference)
    - [Phase 1 — Guest Routes (No Auth)](#phase-1--guest-routes-no-auth)
      - [Groups](#groups)
      - [Members](#members)
      - [Expenses](#expenses)
      - [Summary \& Settlement](#summary--settlement)
    - [Phase 2 — Authenticated Routes (JWT)](#phase-2--authenticated-routes-jwt)
      - [Auth](#auth)
      - [User Profile](#user-profile)
      - [Authenticated Group Creation](#authenticated-group-creation)
  - [Split Calculation Engine](#split-calculation-engine)
    - [How it works](#how-it-works)
    - [Example](#example)
    - [Edge cases](#edge-cases)
  - [Authentication \& Security](#authentication--security)
  - [Error Handling](#error-handling)
  - [Response Conventions](#response-conventions)
  - [Running Tests](#running-tests)
  - [Data Model](#data-model)

---

## Overview

FairShare lets a group of people track shared expenses and instantly calculate who owes what to whom. No account is required — a shareable **GuestToken** (UUID in the URL) is all you need to manage a group. Authenticated accounts unlock ownership, group history, and cross-device access.

**Key capabilities:**

- Create expense groups with a single POST — no signup required
- Add members with optional per-member spending caps
- Record expenses with multiple payers and automatic split calculation
- Get a settlement summary: the minimum number of transfers to zero out all debts
- Promote a guest group to an authenticated account at any time

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Database | PostgreSQL 16 (via Docker) |
| ORM | Entity Framework Core 8 — code-first, Npgsql provider |
| Architecture | Clean Architecture — 4-project solution |
| Validation | FluentValidation 11 |
| Authentication | JWT Bearer (15-min access token) + opaque refresh tokens (7-day) |
| Password hashing | BCrypt.Net-Next (work factor 12) |
| Logging | Serilog — console + rolling daily file |
| API docs | Swashbuckle 6 (OpenAPI 3.0) + Scalar UI |
| Testing | xUnit + FluentAssertions |

---

## Solution Structure

```
FairShare.sln
├── src/
│   ├── FairShare.Domain          # Entities, SplitCalculator — zero dependencies
│   ├── FairShare.Application     # Use cases, DTOs, Interfaces, FluentValidation validators
│   ├── FairShare.Infrastructure  # EF Core DbContext, Repositories, JWT & BCrypt services
│   └── FairShare.API             # Controllers, ExceptionMiddleware, Program.cs
└── tests/
    └── FairShare.Tests           # SplitCalculator unit tests (15 tests)
```

**Dependency rule:** `Domain` ← `Application` ← `Infrastructure` ← `API`

---

## Prerequisites

| Tool | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 or later |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Any recent version |
| [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) | 8.0.x |

Install the EF tools if you haven't already:

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
```

---

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd fairshare-api
```

### 2. Configure environment variables

```bash
cp .env.example .env
```

The defaults in `.env` work out of the box for local development — no changes needed unless you want a different port or credentials.

### 3. Start the database

```bash
docker compose up -d
```

This starts a PostgreSQL 16 container named `fairshare-db` on port **5432**, with a named volume `fairshare_pgdata` so your data survives container restarts.

Verify it's healthy:

```bash
docker compose ps
```

Wait until the `STATUS` column shows `healthy` before running migrations.

### 4. Apply database migrations

```bash
dotnet ef database update \
  --project src/FairShare.Infrastructure \
  --startup-project src/FairShare.API
```

### 5. Run the API

```bash
dotnet run --project src/FairShare.API
```

The API starts on `http://localhost:5243` (or the port shown in the console output).

### 6. Open the interactive docs

Navigate to **`http://localhost:5243/scalar/v1`** in your browser for the Scalar UI (development only).

The raw OpenAPI spec is available at `http://localhost:5243/openapi/v1.json`.

---

## Configuration

All settings live in `src/FairShare.API/appsettings.Development.json` for local development. The file is committed with safe default values.

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fairshare_dev;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key":      "dev-secret-key-replace-in-production-must-be-32chars",
    "Issuer":   "FairShare",
    "Audience": "FairShareClients"
  }
}
```

> **Production:** replace `Jwt:Key` with a cryptographically random string of at least 32 characters. Never commit production secrets — use environment variables or a secrets manager.

### Docker environment variables (`.env`)

| Variable | Default | Description |
|---|---|---|
| `POSTGRES_USER` | `postgres` | DB superuser name |
| `POSTGRES_PASSWORD` | `postgres` | DB superuser password |
| `POSTGRES_DB` | `fairshare_dev` | Database name to create on first start |
| `POSTGRES_PORT` | `5432` | Host port mapped to the container |

### Useful Docker commands

```bash
# Start the database
docker compose up -d

# Stop (data preserved in named volume)
docker compose down

# Stop and wipe all data
docker compose down -v

# View database logs
docker compose logs -f postgres
```

---

## API Reference

All responses use `camelCase` JSON. All IDs are strings (GUIDs). All dates are ISO 8601 UTC. Amounts are decimals rounded to 2 places.

### Phase 1 — Guest Routes (No Auth)

A **GuestToken** (UUID) is returned when you create a group and is used in every subsequent URL for that group. No login required.

#### Groups

```
POST   /api/groups
GET    /api/groups/{guestToken}
```

**POST `/api/groups`** — Create a group

```jsonc
// Request body
{ "name": "Bali Trip", "currency": "LKR" }

// Response 201
{
  "groupId":    "3fa85f64-...",
  "guestToken": "7c9e6679-...",   // ← share this link with your group
  "name":       "Bali Trip",
  "currency":   "LKR"
}
```

**GET `/api/groups/{guestToken}`** — Full group detail including members and expenses.

---

#### Members

```
POST    /api/groups/{guestToken}/members
PATCH   /api/groups/{guestToken}/members/{memberId}
DELETE  /api/groups/{guestToken}/members/{memberId}
```

**POST** — Add a guest member

```jsonc
// Request body
{ "guestName": "Alice", "budgetCap": 5000.00 }
// budgetCap is optional. When set, Alice's maximum fair share is 5000.00
```

**PATCH** — Update name or cap (all fields optional)

```jsonc
{ "guestName": "Alice K.", "budgetCap": 6000.00 }
```

**DELETE** — Removes a member. Returns `409 Conflict` if the member has any associated expense records.

---

#### Expenses

```
POST    /api/groups/{guestToken}/expenses
PUT     /api/groups/{guestToken}/expenses/{expenseId}
DELETE  /api/groups/{guestToken}/expenses/{expenseId}
```

**POST** — Add an expense

```jsonc
// Request body
{
  "title":       "Hotel",
  "totalAmount": 30000.00,
  "payers": [
    { "memberId": "uuid-alice", "amountPaid": 30000.00 }
  ]
}
// Sum of payers[].amountPaid must equal totalAmount
```

**DELETE** — Soft-deletes the expense (`IsDeleted = true`). Never physically removed.

---

#### Summary & Settlement

```
GET /api/groups/{guestToken}/summary
```

Runs the split calculation engine and returns:

```jsonc
{
  "totalSpent": 90000.00,
  "memberBalances": [
    { "memberId": "...", "displayName": "Alice", "balance":  60000.00, "isCreditor": true  },
    { "memberId": "...", "displayName": "Bob",   "balance": -30000.00, "isCreditor": false },
    { "memberId": "...", "displayName": "Carol", "balance": -30000.00, "isCreditor": false }
  ],
  "settlements": [
    { "fromMemberId": "...", "from": "Bob",   "toMemberId": "...", "to": "Alice", "amount": 30000.00 },
    { "fromMemberId": "...", "from": "Carol", "toMemberId": "...", "to": "Alice", "amount": 30000.00 }
  ]
}
```

---

### Phase 2 — Authenticated Routes (JWT)

Include the access token in every request header:
```
Authorization: Bearer <accessToken>
```

#### Auth

```
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout      ← requires JWT
```

**POST `/api/auth/register`**

```jsonc
// Request body
{ "name": "Alice", "email": "alice@example.com", "password": "Secret123" }

// Response 201
{
  "accessToken":  "eyJ...",   // JWT, valid 15 minutes
  "refreshToken": "base64...",// opaque, valid 7 days
  "expiresAt":    "2026-02-26T..."
}
```

Password requirements: min 8 chars · at least 1 uppercase · at least 1 digit.

**POST `/api/auth/login`**

```jsonc
{ "email": "alice@example.com", "password": "Secret123" }
// → same AuthResponse shape as register
```

**POST `/api/auth/refresh`**

```jsonc
{ "refreshToken": "base64..." }
// → new AuthResponse with rotated tokens; old refresh token immediately invalidated
```

**POST `/api/auth/logout`** — Invalidates the current refresh token. Subsequent refresh calls fail.

---

#### User Profile

```
GET   /api/users/me
PATCH /api/users/me
GET   /api/users/me/groups
```

**GET `/api/users/me`**

```jsonc
{ "userId": "...", "name": "Alice", "email": "alice@example.com", "createdAt": "..." }
```

**PATCH `/api/users/me`** — Partial update (all fields optional)

```jsonc
{ "name": "Alice K.", "email": "alicek@example.com" }
// Returns 409 if the new email is already registered to another account
```

**GET `/api/users/me/groups`** — All groups the user owns or belongs to

```jsonc
{ "data": [ { "groupId": "...", "guestToken": "...", "name": "...", "currency": "..." } ], "count": 1 }
```

---

#### Authenticated Group Creation

`POST /api/groups` — same endpoint as Phase 1. When called with a valid JWT the group is owned by the authenticated user (`createdByUserId` is set). Guest token is always generated for share-link access.

---

## Split Calculation Engine

The `SplitCalculator` (`src/FairShare.Domain/Services/SplitCalculator.cs`) is a **pure static domain service** with zero infrastructure or EF dependencies — fully unit-testable in isolation.

Split mode is determined at the **group level** through member `BudgetCap` values, not per-expense. Expenses simply record who paid what; the calculator works out fair shares and settlements across all expenses at once.

### How it works

Given all group members (with their caps and total amounts paid across all expenses) and the total spent, the algorithm runs in 9 steps:

| Step | What happens |
|---|---|
| 1 | `totalSpent` = sum of all active expense amounts |
| 2 | Partition members: **capped** (have `BudgetCap`) vs **uncapped** |
| 3 | `cappedTotal` = sum of all caps |
| 4 | If `cappedTotal >= totalSpent` → ignore all caps, fall back to full equal split |
| 5 | `equalShare = (totalSpent − cappedTotal) / uncappedCount`; remainder assigned to first uncapped member |
| 6 | Capped members owe their `BudgetCap`; uncapped members owe `equalShare` |
| 7 | `balance = amountPaid − fairShare` — positive = creditor, negative = debtor |
| 8 | **Greedy debt simplification** — repeatedly match largest creditor with largest debtor; minimises total number of transfers |
| 9 | Return `MemberBalance[]` + `Settlement[]` |

### Example

Three people, total spent = LKR 9,000. Alice paid it all, Bob and Carol paid nothing.

```
Equal share: 3,000 each
Alice:  9,000 paid − 3,000 owed = +6,000  (creditor)
Bob:    0     paid − 3,000 owed = −3,000  (debtor)
Carol:  0     paid − 3,000 owed = −3,000  (debtor)

Settlements:
  Bob   → Alice  3,000
  Carol → Alice  3,000
```

### Edge cases

| Scenario | Behaviour |
|---|---|
| `cappedTotal >= totalSpent` | All caps ignored; full equal split |
| Member paid more than their cap | Treated as a creditor — excess counts toward offsetting debtors |
| Single member | No settlements returned |
| Zero total spent | All balances are zero |
| Rounding (e.g. 10 ÷ 3) | Remainder assigned to first eligible member; net balance always sums to zero |

---

## Authentication & Security

| Concern | Detail |
|---|---|
| Access token | Signed JWT — 15-minute lifetime, `ClockSkew = Zero` |
| Refresh token | 64-byte random string, 7-day lifetime, rotated on every use |
| Refresh token storage | SHA-256 hex hash stored in DB; plain token sent to client only |
| Password hashing | BCrypt, work factor 12 |
| JWT validation | Issuer, audience, lifetime, and HMAC-SHA256 signature all validated |
| Logout | Nulls the stored refresh token hash — all sessions for that user immediately invalidated |

---

## Error Handling

All errors follow [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807):

```jsonc
{
  "type":    "https://tools.ietf.org/html/rfc7807",
  "title":   "One or more validation errors occurred.",
  "status":  400,
  "detail":  "See 'errors' for field-level details.",
  "traceId": "00-abc123...",
  "errors": {
    "title":       ["Title is required."],
    "totalAmount": ["Total amount must be greater than 0."]
  }
}
```

| Status | When |
|---|---|
| `400` | FluentValidation failure — field errors included |
| `401` | Missing / expired JWT, or wrong login credentials |
| `404` | Unknown `guestToken`, `memberId`, or `expenseId` |
| `409` | Email already registered; or member deletion blocked by existing expenses |
| `500` | Unhandled exception — safe message returned, full stack trace logged |

---

## Response Conventions

| Convention | Rule |
|---|---|
| Property naming | `camelCase` |
| IDs | GUID strings |
| Dates | ISO 8601 UTC — e.g. `"2026-02-26T10:00:00.0000000Z"` |
| Amounts | `decimal`, 2 decimal places |
| Enums | String values — e.g. `"Equal"`, not `0` |
| List responses | `{ "data": [...], "count": N }` |

---

## Running Tests

```bash
dotnet test tests/FairShare.Tests
```

The test suite covers 15 scenarios for `SplitCalculator`:

- Equal split with one payer / multiple payers
- Capped split with one and multiple capped members
- Fallback to equal split when caps exceed total
- Member paid more than their cap (becomes creditor)
- Rounding — net balance always sums to zero
- Edge cases: no members, single member, zero total, all members balanced

---

## Data Model

```
User ──────────────────────────────┐
  id, name, email                  │  owns (nullable)
  passwordHash                     │
  refreshToken (SHA-256 hash)      ▼
  refreshTokenExpiry          Group
  createdAt                     id, name
                                guestToken (unique index)
                                createdByUserId (nullable FK → User)
                                currency, createdAt
                                isArchived ← global query filter
                                      │
                        ┌─────────────┤
                        │             │
                   GroupMember     Expense
                     id              id
                     groupId (FK)    groupId (FK)
                     userId          title, totalAmount
                      (nullable      createdByMemberId (FK → GroupMember)
                       FK → User)    createdAt
                     guestName       isDeleted ← global query filter
                     budgetCap             │
                     joinedAt              │
                        │      ┌───────────┴──────────┐
                        │      │                      │
                        │  ExpensePayer          ExpenseSplit
                        │    id                    id
                        └──► expenseId (FK)        expenseId (FK)
                             memberId  (FK) ◄───── memberId  (FK)
                             amountPaid             shareAmount
```

**Soft-delete strategy**
- `Expense.IsDeleted = true` — excluded via EF Core global query filter
- `Group.IsArchived = true` — excluded via EF Core global query filter
- Members are hard-deleted but blocked if they have any linked expense records

---

## Implementation Notes

### What changed from the original design

| Item | Decision |
|---|---|
| Per-expense `SplitMode` field | **Removed.** Expenses record who paid what — split fairness is a group-level concern handled by member `BudgetCap` values in the `SplitCalculator`. Storing a mode per expense added complexity with no functional effect. Migration `RemoveSplitMode` dropped the column. |
| `?createdByMemberId` query param on `POST /expenses` | **Deferred to Phase 2.** In the guest flow there is no identity, so the field cannot be reliably populated. The column remains on the `Expense` entity for Phase 2 use. |

### Remaining open item

| Item | Status |
|---|---|
| "Settle Group Now" button (frontend) | UI stub only — shows a toast but makes no API call and writes nothing to the database. Settlement in the real world means group members pay each other directly; the app tracks the recommended transfers but does not record the act of payment. A future `POST /api/groups/{guestToken}/archive` endpoint could close the group once debts are settled. |

---

*Built with ASP.NET Core 8 · PostgreSQL 16 · EF Core 8 · Clean Architecture*
