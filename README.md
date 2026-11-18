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
  - [Security](#security)
    - [Who can do what](#who-can-do-what)
    - [Rate limits (per client IP)](#rate-limits-per-client-ip)
    - [Size limits](#size-limits)
    - [Response headers](#response-headers)
    - [Errors and logs](#errors-and-logs)
    - [Not covered](#not-covered)
  - [Error Handling](#error-handling)
  - [Response Conventions](#response-conventions)
  - [Running Tests](#running-tests)
  - [Data Model](#data-model)
  - [Deployment](DEPLOYMENT.md)

---

## Overview

FairShare lets a group of people track shared expenses and instantly calculate who owes what to whom. No account is required — a shareable **GuestToken** (UUID in the URL) is all you need to manage a group. Authenticated accounts unlock ownership, group history, and cross-device access.

**Key capabilities:**

- Create expense groups with a single POST — no signup required
- Add members to a group
- Record expenses with multiple payers and automatic split calculation
- Get a settlement summary: the fewest transfers needed to zero out all debts
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
  },
  "AllowedOrigins": "http://localhost:5173"   // comma-separated CORS origins (the Vite dev server)
}
```

> **CORS:** only the origins listed in `AllowedOrigins` may call the API from a browser. The base `appsettings.json` does not set it, so outside Development no cross-origin requests are allowed until you configure it.

> **Production:** set `Jwt__Key` to a random string of at least 32 characters. The API refuses to start with a missing, short or placeholder key. Never commit production secrets — use environment variables or a secrets manager.

> **Limits:** rate limits (`RateLimit:*`) and group sizes (`Limits:*`) have defaults sized for a dozen friends and can be changed without code changes; see [Security](#security).

> **Deploying:** see [DEPLOYMENT.md](DEPLOYMENT.md) for the free demo setup (Cloudflare Pages + Render + Neon), every environment variable, rate limits and the `/health` endpoint.

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
{ "guestName": "Alice" }
```

**PATCH** — Update a member's name

```jsonc
{ "guestName": "Alice K." }
```

Returns `409 Conflict` when the group already has the most people it can have (30 by default; see [Size limits](#size-limits)).

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
  ],
  "participantMemberIds": ["uuid-alice", "uuid-bob", "uuid-carol"]   // optional — omit to split among everyone
}
// Sum of payers[].amountPaid must equal totalAmount
// Every payer and participant must be a member of the group (400 otherwise)
// 409 when the group already has the most expenses it can have (1,000 by default)
```

**PUT** — Update an expense. Every field is optional; omitted fields are kept (omitting `participantMemberIds` keeps the current participants). Splits are always recalculated. The payers must still sum to the total after the update: sending only `totalAmount` or only `payers` returns `400` (`errors.Payers`) if the result no longer balances.

**DELETE** — Permanently deletes the expense; its payer and split rows are removed by cascade.

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
  ],
  "detailedSettlements": [
    {
      "expenseId": "...", "expenseTitle": "Hotel",
      "settlements": [
        { "fromMemberId": "...", "from": "Bob", "toMemberId": "...", "to": "Alice", "amount": 30000.00 }
      ]
    }
  ]
}
```

- `settlements` — group-wide, netted across all expenses (fewest transfers).
- `detailedSettlements` — the same minimal matching run per expense, with no cross-expense netting, so every pairing is between people who actually shared that expense. Expenses that need no transfers are omitted.

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

Each expense is split equally among the members selected as its participants, and the shares are stored as `ExpenseSplit` rows (see `SplitCalculator.SplitEvenly`). The summary then works out balances and settlements across all expenses at once.

### How it works

| Step | What happens |
|---|---|
| 1 | `totalSpent` = sum of all expense amounts |
| 2 | **Paid** per member = sum of their `ExpensePayer.AmountPaid` rows |
| 3 | **Fair share** per member = sum of their `ExpenseSplit.ShareAmount` rows (members excluded from an expense have no split row for it) |
| 4 | `balance = paid − fairShare` — positive = creditor, negative = debtor |
| 5 | **Minimal settlement** — split the non-zero balances into the largest number of groups that each net to zero, then settle each group by matching its largest creditor with its largest debtor. This gives the fewest possible transfers (n − number of groups); exact for up to 16 non-zero balances, plain greedy matching (at most n − 1) beyond that |
| 6 | Return `MemberBalance[]` + `Settlement[]` |

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
| Single member | No settlements returned |
| Zero total spent | All balances are zero |
| Rounding (e.g. 10 ÷ 3) | Shares are whole cents; leftover cents go one each to the earliest-joined participants, so shares always sum to the total and balances net to zero |

---

## Security

FairShare is built for a small circle of friends (about a dozen people) who share one group link
each. There are no accounts in Phase 1: **anyone with a group's link can view and change that
group**, and the API is public. The measures below are sized for that: generous enough that
friends never notice them, even when they're all on one Wi-Fi network or phone hotspot (and so
share one IP address), while capping what a leaked link or a script can do.

### Who can do what

| Concern | How it's handled |
|---|---|
| Finding a group | Group links use a random UUID (122 random bits), and no endpoint lists groups, so a link can't be guessed. |
| Changing a group | Anyone holding its link. Deleting an expense is permanent, so share links only with the people in the group. |
| Accounts (Phase 2) | JWT access tokens (15 minutes, `ClockSkew = Zero`; issuer, audience, lifetime and HMAC-SHA256 signature validated). Refresh tokens are 64 random bytes, valid 7 days, rotated on every use and stored only as a SHA-256 hash. Passwords use BCrypt (work factor 12). Logout clears the stored refresh token. |
| Secrets | `Jwt__Key` must be at least 32 random characters; the API refuses to start with a missing, short or placeholder key. Production secrets live in environment variables, never in the repo. |

### Rate limits (per client IP)

| Limit | Default | Setting | Why this number |
|---|---|---|---|
| Reads | 300 a minute | `RateLimit:ReadsPerMinute` | A dozen friends on one network opening the group at once use a few dozen. |
| Writes | 60 a minute | `RateLimit:WritesPerMinute` | Everyone adding the day's expenses at once. |
| Writes | 1,000 a day | `RateLimit:WritesPerDay` | The guard on database growth: without it, 60 a minute would still allow 80,000+ a day. A busy day for the whole group is a few hundred. |
| New groups | 10 an hour | `RateLimit:GroupsPerHour` | Friends make one group per trip. |

Writes are POST, PUT, PATCH and DELETE, and refused requests count too. `/health` is never
limited. Over a limit, the API answers `429` with a `Retry-After` header and a title people can
read, e.g. *"Too many requests from this network. Try again in 25 minutes."* (the web app shows it
as-is). Counts are kept in memory, which suits the single free instance; with several instances
they'd need a shared store. The client IP comes from the hosting proxy's `X-Forwarded-For` (one
trusted hop), so the API must only be reachable through that proxy.

### Size limits

| Limit | Default | Setting | Over the limit |
|---|---|---|---|
| Request body | 64 KB | fixed | `413` *"That request is too large."* The largest real request (an expense split between 30 people) is about 5 KB. |
| People per group | 30 | `Limits:MembersPerGroup` (1 to 100) | `409` *"This group already has 30 people, the most a group can have."* |
| Expenses per group | 1,000 | `Limits:ExpensesPerGroup` | `409` *"This group already has 1,000 expenses, the most a group can have. Start a new group for new expenses."* That's months of heavy use for a dozen people. |
| Payers or participants in one expense | 100 | fixed ceiling | `400` on `Payers` / `ParticipantMemberIds`. Every ID must also belong to the group and appear only once. |
| Text | Group name 100, person 80, expense title 150, currency 10 characters | fixed | `400` |

Together these bound what one IP can store in a day (at most 1,000 writes of a few rows each)
and keep every group small enough that its summary stays cheap to work out.

### Response headers

Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer` and `Content-Security-Policy: default-src 'none';
frame-ancestors 'none'` (a JSON response can't load anything or be framed). `/api` responses add
`Cache-Control: no-store`, so group data isn't left in browser or proxy caches. Outside
Development the API sends HSTS on HTTPS requests, and it never sends a `Server` header. The
dev-only API reference pages (Scalar, OpenAPI JSON) skip the content security policy so they
load. The web app sets its own headers; see its README.

### Errors and logs

Unexpected errors return a generic `500` with a `traceId`; details and stack traces go only to the
logs. Malformed or oversized requests get their own status (`400`, `413`) and are logged as
warnings, not errors. CORS only allows the origins in `AllowedOrigins`.

### Not covered

- **Many IPs at once.** Every limit is per IP, so traffic spread over many addresses adds up. Put
  the API behind Cloudflare (free) if that ever becomes a problem.
- **Undo.** A deleted expense can't be recovered, and changes aren't attributed to anyone.
- **Login attempts** share the general write limits; there's no per-account lockout yet.
- **Logs** include request paths, and a group's path contains its link.

---|---|
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
  "traceId": "00-abc123...",
  "errors": {
    "Title":                ["Title is required."],
    "TotalAmount":          ["Total amount must be greater than 0."],
    "Payers[0].AmountPaid": ["Amount paid can have at most 2 decimal places."]
  }
}
```

> **Note:** `errors` keys are the C# property paths in **PascalCase** (`Title`, `TotalAmount`, `Payers[0].AmountPaid`, `ParticipantMemberIds`, `GuestName`) — the camelCase naming policy does not apply to dictionary keys. Match them case-insensitively on the client.

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
| Enums | Serialised as strings (`JsonStringEnumConverter`); no current response contains an enum |
| Top-level list responses | `{ "data": [...], "count": N }` (e.g. `GET /api/users/me/groups`); lists nested inside an object are plain arrays |

---

## Running Tests

```bash
dotnet test tests/FairShare.Tests
```

The test suite covers `SplitCalculator` and expense payer validation:

- Equal split with one payer / multiple payers
- Participant exclusion (excluded members owe nothing for that expense)
- Rounding — whole-cent shares that sum to the total; net balance always sums to zero
- Payer and participant validation — malformed, duplicate and out-of-group member ids are rejected
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
                     guestName             │
                     joinedAt              │
                        │      ┌───────────┴──────────┐
                        │      │                      │
                        │  ExpensePayer          ExpenseSplit
                        │    id                    id
                        └──► expenseId (FK)        expenseId (FK)
                             memberId  (FK) ◄───── memberId  (FK)
                             amountPaid             shareAmount
```

**Delete strategy**
- Expenses are hard-deleted; their payer and split rows are removed by cascade
- `Group.IsArchived = true` — excluded via EF Core global query filter
- Members are hard-deleted but blocked if they have any linked expense records

---

## Implementation Notes

### What changed from the original design

| Item | Decision |
|---|---|
| Per-expense `SplitMode` field | **Removed.** Every expense is split equally among its selected participants; storing a mode per expense added complexity with no functional effect. Migration `RemoveSplitMode` dropped the column. |
| `?createdByMemberId` query param on `POST /expenses` | **Deferred to Phase 2.** In the guest flow there is no identity, so the field cannot be reliably populated. The column remains on the `Expense` entity for Phase 2 use. |

### Remaining open item

| Item | Status |
|---|---|
| Settling / archiving groups | Out of scope for version 1: FairShare works out the fewest payments that make everyone even, and people pay each other outside the app. Nothing is recorded as paid, and the former "Settle Group Now" UI stub was removed. A future `POST /api/groups/{guestToken}/archive` endpoint could close a group in a later version. |

---

*Built with ASP.NET Core 8 · PostgreSQL 16 · EF Core 8 · Clean Architecture*
