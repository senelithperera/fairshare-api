# FairShare — Feature Catalogue

A complete map of every feature implemented in this project, organised by layer and phase.

---

## Table of Contents

1. [Phase 1 — Guest (No Auth) Features](#phase-1--guest-no-auth-features)
2. [Phase 2 — Authenticated Features](#phase-2--authenticated-features)
3. [Split Calculation Engine](#split-calculation-engine)
4. [Cross-Cutting Concerns](#cross-cutting-concerns)
5. [Data Model](#data-model)

---

## Phase 1 — Guest (No Auth) Features

All Phase 1 routes are accessed via a `guestToken` (UUID) in the URL path instead of authentication. No login is required.

### Group Management

| Feature | Method | Route |
|---|---|---|
| Create a guest group | `POST` | `/api/groups` |
| Get full group detail | `GET` | `/api/groups/{guestToken}` |

**Create group** auto-generates a unique `GuestToken` (UUID) used as the group's shareable public identifier. Returns `groupId`, `guestToken`, `name`, and `currency`.

**Get group detail** returns the group's name, currency, creation time, archival status, all members (with total amounts paid), and all expenses with their payer and split breakdowns.

---

### Member Management

| Feature | Method | Route |
|---|---|---|
| Add a guest member | `POST` | `/api/groups/{guestToken}/members` |
| Update a member's name | `PATCH` | `/api/groups/{guestToken}/members/{memberId}` |
| Remove a member | `DELETE` | `/api/groups/{guestToken}/members/{memberId}` |

- **Add member** — requires `guestName`.
- **Update member** — changes the member's `guestName`.
- **Delete member** — hard-deletes the record but is blocked with HTTP 409 if the member has any associated expense records (payer or split), preventing data orphaning.

---

### Expense Management

| Feature | Method | Route |
|---|---|---|
| Add an expense | `POST` | `/api/groups/{guestToken}/expenses` |
| Update an expense | `PUT` | `/api/groups/{guestToken}/expenses/{expenseId}` |
| Delete an expense | `DELETE` | `/api/groups/{guestToken}/expenses/{expenseId}` |

- **Add expense** — records `title`, `totalAmount`, and one or more `payers` (member + amount paid). Validates that payer amounts sum to `totalAmount`. Calculates and persists `ExpenseSplit` records (equal division per expense) immediately.
- **Update expense** — partial update for title and/or total amount; replaces payers if provided and recalculates splits.
- **Delete expense** — hard delete. The expense row is removed and its payer and split rows go with it (ON DELETE CASCADE).

Every expense is split equally among its selected participants (`participantMemberIds`, default everyone). Expenses record who paid what and who shares the cost; the `SplitCalculator` nets this across all expenses at summary time.

---

### Group Summary & Settlement

| Feature | Method | Route |
|---|---|---|
| Get financial summary | `GET` | `/api/groups/{guestToken}/summary` |

Returns:
- `totalSpent` — sum of all active expense amounts
- `memberBalances` — per-member balance: positive = creditor (is owed money), negative = debtor (owes money)
- `settlements` — the minimum set of cash transfers to fully settle the group (greedy algorithm)

---

## Phase 2 — Authenticated Features

All Phase 2 routes require a valid **JWT Bearer token** in the `Authorization` header.

### Authentication

| Feature | Method | Route |
|---|---|---|
| Register a new account | `POST` | `/api/auth/register` |
| Login | `POST` | `/api/auth/login` |
| Refresh access token | `POST` | `/api/auth/refresh` |
| Logout | `POST` | `/api/auth/logout` |

**Token design:**
- **Access token** — signed JWT, expires in **15 minutes**. Contains `sub` (user ID), `email`, `name`, `jti`.
- **Refresh token** — cryptographically random 64-byte opaque string, expires in **7 days**. Stored as a SHA-256 hash in the database. Rotated on every refresh call (old token is immediately invalidated).
- **Logout** — nulls out the stored refresh token hash, invalidating all sessions for that user.

**Password rules:** minimum 8 characters, at least one uppercase letter, at least one digit.

---

### User Profile

| Feature | Method | Route |
|---|---|---|
| Get current user profile | `GET` | `/api/users/me` |
| Update name or email | `PATCH` | `/api/users/me` |
| Get all groups for user | `GET` | `/api/users/me/groups` |

- **Update profile** — partial update; validates new email is not already taken by another account (HTTP 409 on conflict).
- **My groups** — returns all non-archived groups the user owns or participates in as a member. Wrapped in `{ data: [...], count: N }`.

---

### Authenticated Group Creation

| Feature | Method | Route |
|---|---|---|
| Create an owned group (JWT) | `POST` | `/api/groups` |

Same endpoint as Phase 1 — when a valid JWT is present, the group's `CreatedByUserId` is set to the authenticated user's ID. When called without auth, it creates a guest group (null `CreatedByUserId`). Every group always receives a `GuestToken` for share-link access.

---

## Split Calculation Engine

The `SplitCalculator` is a **pure static domain service** (`FairShare.Domain/Services/SplitCalculator.cs`) with zero infrastructure dependencies, designed to be fully unit-testable.

Each expense's shares are computed when the expense is saved (`SplitCalculator.SplitEvenly`, whole cents) and stored as `ExpenseSplit` rows. The summary aggregates payments and shares across all expenses and computes net balances in one pass.

### Algorithm

| Step | Action |
|---|---|
| 1 | Sum all expense amounts → `totalSpent` |
| 2 | **Paid** per member = sum of their `ExpensePayer.AmountPaid` rows |
| 3 | **Fair share** per member = sum of their `ExpenseSplit.ShareAmount` rows (no row = excluded from that expense) |
| 4 | `balance = amountPaid - fairShare` (positive = creditor, negative = debtor) |
| 5 | **Greedy debt simplification** — repeatedly match the largest creditor with the largest debtor, record a transfer, reduce balances; at most n − 1 transfers |
| 6 | Return `MemberBalance[]` and `Settlement[]` |

### Edge cases handled

| Scenario | Behaviour |
|---|---|
| No members | Returns empty result |
| Single member | No settlements needed |
| Zero total spent | All balances are zero |
| All members paid equally | No settlements needed |
| Member excluded from an expense | Owes nothing for it; can still be a payer (pure creditor) |
| Decimal rounding | Shares are whole cents; leftover cents go one each to the earliest-joined participants, so shares sum exactly to the total |
| Multiple creditors and debtors | Greedy algorithm minimises transaction count |

### Unit test coverage

`FairShare.Tests/Domain/SplitCalculatorTests.cs`

| Test | Scenario |
|---|---|
| `EqualSplit_ThreeMembers_BalancesAreCorrect` | One payer, two non-payers — correct balance split |
| `EqualSplit_ThreeMembers_SettlementsAreMinimal` | Two settlements, both to Alice |
| `EqualSplit_TwoMembers_BothPaidSome` | Partial payers — correct creditor/debtor assignment |
| `Exclusion_MemberNotInSplit_OwesNothingForThatExpense` | Excluded member has zero balance and no settlement |
| `Exclusion_ExcludedMemberStillPaid_IsPureCreditor` | Payer excluded from the split is owed the full amount |
| `SplitEvenly_ProducesWholeCentSharesThatSumToTotal` | Whole-cent shares that sum exactly to the total |
| `SplitEvenly_ZeroMembers_Throws` | Splitting across zero members is rejected |
| `EqualSplit_Rounding_BalancesNetToExactlyZero` | Balances net to zero; each settlement matches the balance |
| `Calculate_NoMembers_ReturnsEmptyResult` | Empty input → empty output |
| `Calculate_OneMember_NoSettlementsNeeded` | Solo group → no settlements |
| `Calculate_ZeroTotalSpent_AllBalancesZero` | Nothing spent → all balances zero |
| `EqualSplit_AllMembersPaidEqual_NoSettlements` | Perfect split → no settlements |
| `SimplifyDebts_MultipleCreditors_TransactionsMinimized` | Greedy algorithm settles all debts exactly |

`FairShare.Tests/Application/ExpenseMemberIdValidationTests.cs` covers payer and participant ids: malformed and duplicate ids are rejected by the validators, and ids outside the group are rejected by the create/update use cases without saving (no more falling back to "everyone" for unknown participants).

---

## Cross-Cutting Concerns

### Validation (FluentValidation)

All request bodies are validated before reaching use cases. Validation errors return HTTP 400 with RFC 7807 `ProblemDetails` including field-level error messages.

| Validator | Rules |
|---|---|
| `CreateGroupValidator` | Name: required, max 100; Currency: required, max 10 |
| `AddMemberValidator` | GuestName: required, max 80 |
| `UpdateMemberValidator` | GuestName: max 80 if provided |
| `CreateExpenseValidator` | Title: required, max 150; Amount: > 0, max 2 decimals; at least one payer; payer amounts must sum to total; payer and participant ids must be valid GUIDs with no duplicates |
| `UpdateExpenseValidator` | Title: max 150 if provided; Amount: > 0, max 2 decimals if provided; payer amounts must sum to total if both provided; payer and participant ids must be valid GUIDs with no duplicates |
| `RegisterValidator` | Name: required, max 100; Email: valid format, max 255; Password: min 8, 1 uppercase, 1 digit |
| `LoginValidator` | Email: required, valid format; Password: required |

---

### Error Handling

- **Global exception middleware** (`ExceptionMiddleware.cs`) catches all unhandled exceptions, logs the full stack trace via Serilog, and returns a safe RFC 7807 `ProblemDetails` response with HTTP 500.
- All domain-level errors (not found, conflict, unauthorized) are returned as `ProblemDetails` directly from controllers.

| HTTP Status | When |
|---|---|
| `400 Bad Request` | FluentValidation failure — includes per-field errors |
| `401 Unauthorized` | Missing/invalid JWT or wrong credentials |
| `404 Not Found` | Invalid `guestToken`, `memberId`, or `expenseId` |
| `409 Conflict` | Email already exists; member has expenses and cannot be deleted |
| `500 Internal Server Error` | Unhandled exception — safe message returned, full trace logged |

---

### Logging (Serilog)

- Structured JSON-enriched logging via Serilog.
- **Sinks:** console (development) + rolling daily log files (`logs/fairshare-YYYYMMDD.log`, retained 14 days).
- Log level configurable per-environment via `appsettings.{Environment}.json`.
- Correlation via `Enrich.FromLogContext()` — request trace IDs included in all log entries.

---

### API Documentation

- **Swashbuckle 6** generates the OpenAPI 3.0 spec at `/openapi/v1.json`.
- **Scalar UI** (development only) served at `/scalar/v1` — interactive, themed (Purple).
- JWT Bearer authentication pre-configured in the Swagger definition.

---

### Security

| Concern | Implementation |
|---|---|
| Password storage | BCrypt with work factor 12 (`BCrypt.Net-Next`) |
| Refresh token storage | SHA-256 hex hash stored in DB; plain token only ever sent to client |
| Token rotation | Refresh token is invalidated and replaced on every use |
| Access token lifetime | 15 minutes (`ClockSkew = Zero`) |
| JWT validation | Issuer, audience, lifetime, and signature all validated |
| CORS | Configurable via default policy; open in dev, should be locked down in production |

---

### Response Conventions

| Convention | Rule |
|---|---|
| Property casing | `camelCase` throughout |
| IDs | All GUIDs returned as strings |
| Dates | ISO 8601 UTC strings (`"o"` format) |
| Amounts | `decimal` with 2 decimal places |
| List responses | Wrapped as `{ "data": [...], "count": N }` |
| Enum values | Serialised as strings (e.g. `"Equal"`, not `0`) |

---

## Data Model

```
User ──────────────────────────────┐
  id, name, email, passwordHash    │ owns
  refreshToken (hash), expiry      │
  createdAt                        ▼
                              Group
                                id, name, guestToken (unique)
                                createdByUserId (nullable FK → User)
                                currency, createdAt, isArchived
                                        │
                          ┌─────────────┤
                          │             │
                     GroupMember    Expense
                       id             id
                       groupId (FK)   groupId (FK)
                       userId (nullable FK → User)
                       guestName      title, totalAmount
                       joinedAt       createdByMemberId (FK → GroupMember)
                                      createdAt
                          │                │
               ┌──────────┘    ┌───────────┴────────────┐
               │               │                        │
        (payer records)  ExpensePayer             ExpenseSplit
                           id                       id
                           expenseId (FK)           expenseId (FK)
                           memberId (FK)            memberId (FK)
                           amountPaid               shareAmount
```

**Delete strategy:**
- Expenses are hard-deleted; payer and split rows are removed by cascade
- `Group.IsArchived = true` — global EF query filter excludes from list reads

---

---

## Implementation Notes

### Changes from original design

| Item | Decision |
|---|---|
| Per-expense `SplitMode` field | **Removed.** The field was accepted in requests and stored in the DB but never influenced any calculation. Migration `RemoveSplitMode` dropped the `SplitMode` column from the `expenses` table. `SplitMode.cs` enum deleted. |
| `?createdByMemberId` query param | **Deferred to Phase 2.** Guest flow has no identity, so the field cannot be reliably populated. The `CreatedByMemberId` column remains on the entity for Phase 2. |
| Dead code in `CreateExpenseUseCase` | **Removed.** The intermediate `SplitCalculator.Calculate()` call and its result were being immediately overwritten by `BuildExpenseSplits()`. Cleaned up — `SplitCalculator` is now only called in `GetSummaryUseCase` where it belongs. |

### Remaining open item

| Item | Status |
|---|---|
| "Settle Group Now" button | Frontend UI stub only. No API endpoint exists. Deferred — a future `POST /api/groups/{guestToken}/archive` could close the group once the group decides debts are settled. |

*Last updated: 2026-06-24*
