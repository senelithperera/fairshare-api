# FairShare — Guest UI Specification

A screen-by-screen description of the user interface required to fully consume the Phase 1 (no-auth) guest APIs.

---

## User Flow Overview

```
Landing Page
    │
    ▼
[Create Group] ──POST /api/groups──► Group Dashboard  (URL: /groups/{guestToken})
                                          │
                         ┌────────────────┼────────────────┐
                         ▼                ▼                ▼
                   [Add Member]     [Add Expense]    [View Summary]
                   [Edit Member]    [Edit Expense]
                   [Delete Member]  [Delete Expense]
```

The `guestToken` UUID is the only key needed to access and manage a group. It lives in the URL and can be shared with others as a link.

---

## Screen 1 — Landing / Create Group

**Purpose:** Entry point. User creates a new expense group.

**API call:** `POST /api/groups`

### Layout

```
┌──────────────────────────────────────────┐
│              FairShare                   │
│   Split expenses with anyone, no login   │
│                                          │
│  Group Name  [________________________]  │
│  Currency    [________________________]  │
│                                          │
│            [ Create Group ]             │
└──────────────────────────────────────────┘
```

### Fields

| Field | Type | Required | Constraints | Notes |
|---|---|---|---|---|
| Group Name | Text input | Yes | Max 100 chars | e.g. "Bali Trip" |
| Currency | Text input or short dropdown | Yes | Max 10 chars | e.g. LKR, USD, EUR |

### Behaviour

- On **Create Group** click: POST `{ name, currency }` to `/api/groups`.
- On success (`201`): navigate to the Group Dashboard at `/groups/{guestToken}`.
- Store the `guestToken` in the URL — this is the shareable link for the group.
- On validation error (`400`): display inline field errors below each input.

---

## Screen 2 — Group Dashboard

**Purpose:** Central view for the group. Shows members and expenses; entry point to all management actions.

**API call:** `GET /api/groups/{guestToken}` (on load)

### Layout

```
┌──────────────────────────────────────────────────────────┐
│  ← Back   Bali Trip  (LKR)              [View Summary]   │
├──────────────────────────────────────────────────────────┤
│  MEMBERS                                   [+ Add Member] │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Alice          Cap: —          Paid: 90,000.00  ✎ │  │
│  │  Bob            Cap: 5,000.00   Paid: 0.00        ✎ │  │
│  │  Carol          Cap: —          Paid: 0.00        ✎ │  │
│  └────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│  EXPENSES                                 [+ Add Expense] │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Hotel       30,000.00   Equal   Alice       ✎ 🗑  │  │
│  │  Transport   15,000.00   Equal   Alice, Bob  ✎ 🗑  │  │
│  │  Food        45,000.00   Capped  Alice       ✎ 🗑  │  │
│  └────────────────────────────────────────────────────┘  │
│                                Total spent: 90,000.00     │
└──────────────────────────────────────────────────────────┘
```

### Data displayed (from GET response)

| Section | Fields |
|---|---|
| Header | Group name, currency |
| Members list | `guestName`, `budgetCap` (shown as "—" if null), total `amountPaid` across all expenses |
| Expenses list | `title`, `totalAmount`, `splitMode`, payer names, edit/delete actions |
| Footer | Sum of all expense `totalAmount` values |

### Actions

| Action | Trigger |
|---|---|
| Open Add Member form | "+ Add Member" button |
| Open Edit Member form | ✎ icon on a member row |
| Open Add Expense form | "+ Add Expense" button |
| Open Edit Expense form | ✎ icon on an expense row |
| Soft-delete expense | 🗑 icon on an expense row (with confirmation prompt) |
| Navigate to summary | "View Summary" button |

---

## Screen 3 — Add / Edit Member (Modal or Slide-in Panel)

**Purpose:** Add a new member to the group, or update an existing member's name or budget cap.

**API calls:**
- Add: `POST /api/groups/{guestToken}/members`
- Edit: `PATCH /api/groups/{guestToken}/members/{memberId}`
- Delete (from edit form): `DELETE /api/groups/{guestToken}/members/{memberId}`

### Layout

```
┌────────────────────────────────┐
│  Add Member          [ × ]     │
├────────────────────────────────┤
│  Name    [____________________]│
│  Budget  [____________________]│
│  Cap     (optional, e.g. 5000) │
│                                │
│  [ Cancel ]   [ Save Member ]  │
│                                │
│  (edit mode only)              │
│  ──────────────────────────── │
│  [ Delete Member ]             │
└────────────────────────────────┘
```

### Fields

| Field | Type | Required | Constraints |
|---|---|---|---|
| Name | Text input | Yes | Max 80 chars |
| Budget Cap | Number input | No | Must be > 0 if provided |

### Behaviour

- **Add mode:** fields start empty.
- **Edit mode:** fields pre-filled with current `guestName` and `budgetCap`.
- On **Save**: call the appropriate endpoint; on success close the form and refresh the member list.
- On **Delete Member** (edit mode only):
  - Show confirmation: *"Remove [Name]? This cannot be undone."*
  - Call `DELETE /api/groups/{guestToken}/members/{memberId}`.
  - If `409 Conflict` is returned: show error — *"This member has recorded expenses and cannot be removed."*

---

## Screen 4 — Add / Edit Expense (Modal or Full Form)

**Purpose:** Record a new expense or modify an existing one.

**API calls:**
- Add: `POST /api/groups/{guestToken}/expenses?createdByMemberId={memberId}`
- Edit: `PUT /api/groups/{guestToken}/expenses/{expenseId}`

### Layout

```
┌─────────────────────────────────────────────┐
│  Add Expense                        [ × ]    │
├─────────────────────────────────────────────┤
│  Title         [_____________________________]│
│  Total Amount  [_____________________________]│
│  Split Mode    [ Equal ▾ ]                   │
│  Added by      [ Alice  ▾ ]                  │
│                                              │
│  WHO PAID?                      [+ Add Payer]│
│  ┌───────────────────────────────────────┐  │
│  │  [ Alice ▾ ]   Amount [ 30,000.00 ]  │  │
│  │  [ Bob   ▾ ]   Amount [  5,000.00 ]  │  │
│  └───────────────────────────────────────┘  │
│                                              │
│  Remaining unallocated: 0.00   ✓ Balanced   │
│                                              │
│  [ Cancel ]           [ Save Expense ]      │
└─────────────────────────────────────────────┘
```

### Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| Title | Text input | Yes | Max 150 chars |
| Total Amount | Number input | Yes | Must be > 0 |
| Split Mode | Dropdown | Yes | Equal / Capped / Custom / Percentage |
| Added by | Dropdown (members) | Yes | Sent as `?createdByMemberId=` query param |
| Payers | Repeating row: member dropdown + amount | Yes | At least one; amounts must sum to Total Amount |

### Payer section behaviour

- "+ Add Payer" appends a new payer row (member dropdown + amount field).
- A running **"Remaining unallocated"** counter shows `totalAmount − sum(payerAmounts)` in real time.
  - Shows green "✓ Balanced" when the difference is 0.
  - Shows red "⚠ X.XX unallocated" when it is not.
- The same member cannot appear twice in the payer list.
- **Save** is disabled until the amounts balance.

### Behaviour

- **Add mode:** fields start empty; one default payer row.
- **Edit mode:** fields pre-filled; payer rows rendered from existing payer data.
- On **Save**: call the appropriate endpoint; on success close the form and refresh the expense list.
- On validation error (`400`): show field-level errors inline.

---

## Screen 5 — Settlement Summary

**Purpose:** Show the final financial picture — who is owed money and the minimum set of payments to settle up.

**API call:** `GET /api/groups/{guestToken}/summary`

### Layout

```
┌─────────────────────────────────────────────────────┐
│  ← Back to Group      Bali Trip — Summary           │
├─────────────────────────────────────────────────────┤
│  Total Spent                             90,000.00  │
├─────────────────────────────────────────────────────┤
│  BALANCES                                           │
│  ┌───────────────────────────────────────────────┐  │
│  │  Alice   +60,000.00  ████████████  (owed)     │  │
│  │  Bob     -30,000.00  ██████        (owes)     │  │
│  │  Carol   -30,000.00  ██████        (owes)     │  │
│  └───────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────┤
│  SETTLE UP                                          │
│  ┌───────────────────────────────────────────────┐  │
│  │  Bob   ──  30,000.00  ──►  Alice              │  │
│  │  Carol ──  30,000.00  ──►  Alice              │  │
│  └───────────────────────────────────────────────┘  │
│                                                     │
│  [ Share Group Link ]   [ Back to Group ]           │
└─────────────────────────────────────────────────────┘
```

### Data displayed

| Section | Source field | Display |
|---|---|---|
| Total Spent | `totalSpent` | Formatted with currency |
| Balances | `memberBalances[]` | Name, formatted balance, visual bar, creditor/debtor label |
| Settle Up | `settlements[]` | "From → amount → To" per transfer |

### Visual treatment for balances

- Creditors (positive balance): green text, label "is owed".
- Debtors (negative balance): red text, label "owes".
- Bar width proportional to absolute balance value relative to `totalSpent`.

### Actions

| Action | Behaviour |
|---|---|
| Share Group Link | Copy `https://<host>/groups/{guestToken}` to clipboard |
| Back to Group | Navigate back to the Group Dashboard |

---

## Shared UX Patterns

### Shareable Link

The `guestToken` in the URL is the only thing needed to re-open a group on any device. Anywhere the user can see the group, a **"Copy Link"** or **"Share"** button should be present.

### Loading & Error States

| State | Handling |
|---|---|
| Loading (GET calls) | Show skeleton or spinner in the relevant section |
| 404 on group load | Full-page error: *"Group not found. Check the link and try again."* |
| 400 validation errors | Inline below each field |
| 409 on member delete | Inline error inside the Edit Member form |
| 500 / network error | Toast notification: *"Something went wrong. Please try again."* |

### Confirmation Prompts

Required before destructive actions:
- Deleting an expense: *"Delete [title]? This cannot be undone."*
- Deleting a member: *"Remove [name]? This cannot be undone."* (then handle 409 gracefully)

---

*Covers Phase 1 guest APIs only — no login required.*
