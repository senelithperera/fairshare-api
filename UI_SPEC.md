# FairShare — Guest UI Specification

A screen-by-screen description of the guest (Phase 1, no-auth) user interface as implemented in
`fairshare-web` (Preact + Vite). For the full UI → API → DB trace of each operation, see
`workspaces/context/phase1-e2e-spec.md`.

---

## User Flow Overview

```
Landing Page  (/)
    │
    ▼
[Create Group] ──POST /api/groups──► Group Page  (URL: /group/{guestToken})
                                          │
                     ┌────────────────────┼─────────────────────┐
                     ▼                    ▼                     ▼
               Members section     Expenses section     Summary sidebar
               [Add / Edit /       [Add / Edit /        (balances +
                Delete Member]      Delete Expense]      settlements)
```

The `guestToken` UUID is the only key needed to access and manage a group. It lives in the URL
and can be shared with others as a link. Any other path shows a "Page not found" screen.

---

## Screen 1 — Landing / Create Group

**Route:** `/` — `src/routes/home.jsx`
**API call:** `POST /api/groups`

### Layout

```
┌──────────────────────────────────────────┐
│              FairShare                   │
│   Organize shared expenses with precision│
│                                          │
│  ┌ Create New Group ───────────────────┐ │
│  │ Group Name  [______________________]│ │
│  │ Currency    [ LKR ▾ ]               │ │
│  │          [ ⊕ Create Group ]         │ │
│  │ ⓘ Share the group link with anyone… │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

### Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| Group Name | Text input | Yes | API max 100 chars |
| Currency | Dropdown | Yes | LKR (default), USD, EUR, GBP, AUD, SGD, INR. The API accepts any string ≤ 10 chars and upper-cases it |

### Behaviour

- Local validation: empty name → "Group name is required".
- On success (`201`): navigate to `/group/{guestToken}`.
- On `400`: field errors shown inline; errors with no matching field are shown as a toast.
- Other failures: error toast.

---

## Screen 2 — Group Page

**Route:** `/group/:guestToken` — `src/routes/group.jsx`
**API calls on load (in parallel):** `GET /api/groups/{guestToken}` and `GET /api/groups/{guestToken}/summary`

Both are re-fetched after every successful add / edit / delete.

### Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│ [LKR] Bali Trip                                        [⧉ Copy Link] │
│ 3 members · 3 expenses                                               │
├───────────────────────────────────────────┬──────────────────────────┤
│ Members                         3 Total   │ ┌ Group Total Spent ───┐ │
│ [+ Add Member]                            │ │ LKR 90,000.00        │ │
│ ┌────────┐ ┌────────┐ ┌────────┐          │ └──────────────────────┘ │
│ │(A)Alice│ │(B) Bob │ │(C)Carol│  ✎ 🗑    │ ┌ Balances ────────────┐ │
│ └────────┘ └────────┘ └────────┘          │ │ Alice  + LKR 60,000  │ │
│                                           │ │ Bob    - LKR 30,000  │ │
│ Expenses                   [+ Add Expense]│ │ Carol  - LKR 30,000  │ │
│ ┌───────────────────────────────────────┐ │ └──────────────────────┘ │
│ │ 🏨 Hotel            LKR 30,000.00 ✎ 🗑 │ │ ┌ Recommended Settle…─┐ │
│ │    Paid by Alice                      │ │ │ (Simplified|Detailed)│ │
│ │    Split between Alice, Bob           │ │ │ Bob → Alice  30,000  │ │
│ └───────────────────────────────────────┘ │ │ Carol → Alice 30,000 │ │
│                                           │ │ [✓ Settle Group Now] │ │
│                                           │ └──────────────────────┘ │
└───────────────────────────────────────────┴──────────────────────────┘
```

On large screens the content is a 6/4 column grid with a sticky sidebar; on small screens the
sidebar stacks below.

### Header

| Element | Source / behaviour |
|---|---|
| Currency badge + group name | `currency`, `name` |
| "N members · N expenses" | counts of `members` / `expenses` |
| Copy Link | Copies `window.location.href` (clipboard API, `execCommand('copy')` fallback); toast "Link copied to clipboard!" |

### Members section

- "Add Member" button opens the member modal (Screen 3).
- One `MemberCard` per member: avatar (initials) + `displayName`, with ✎ (edit) and 🗑 (delete).
  `totalAmountPaid` is not displayed.
- 🗑 → confirm dialog "Delete {name}?" → `DELETE …/members/{memberId}`.
  - `204` → toast "Member removed".
  - `409` → error toast "This member has expenses and cannot be deleted."
- Empty state: "No members yet".

### Expenses section

- "Add Expense" is disabled until at least one member exists.
- One `ExpenseCard` per expense: icon (guessed from keywords in the title), `title`,
  "Paid by {payer names}", `totalAmount`, and — only when fewer than all members take part —
  "Split between {participant names}" (from `splits`).
- ✎ opens the expense modal pre-filled (Screen 4).
- 🗑 → confirm dialog `Delete "{title}"?` → `DELETE …/expenses/{expenseId}` (hard delete, no undo)
  → toast "Expense removed".
- Empty state: "No expenses yet" (with a hint to add a member first if there are none).

### Summary sidebar — `src/components/summary-sidebar.jsx`

| Card | Source | Display |
|---|---|---|
| Group Total Spent | `totalSpent` | Formatted with currency |
| Balances | `memberBalances[]` | Avatar, name, amount. `isCreditor` → green "+"; otherwise red "-" (a zero balance currently renders as "- 0.00") |
| Recommended Settlements | `settlements[]` / `detailedSettlements[]` | Toggle: **Simplified** = group-wide netted transfers; **Detailed** = transfers grouped under each expense title. "From → To  amount" per row. Empty → "🎉 All settled up!" |
| Settle Group Now | — | **UI stub:** spins 600 ms and toasts "All debts marked as settled!". No API call |

Skeleton cards are shown while the summary is loading.

---

## Screen 3 — Add / Edit Member (modal)

**Component:** `src/components/add-member-modal.jsx`
**API calls:** Add `POST /api/groups/{guestToken}/members` · Edit `PATCH /api/groups/{guestToken}/members/{memberId}`

```
┌────────────────────────────────┐
│  Add Member              [ × ] │
├────────────────────────────────┤
│  Name   [____________________] │
│                                │
│  [ 👤 Add Member ]             │   (edit mode: "Save Changes")
└────────────────────────────────┘
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| Name | Text input | Yes | API max 80 chars. Sent as `guestName` |

- **Add mode:** empty. **Edit mode:** pre-filled with `displayName`.
- Close via ×, backdrop click or Escape.
- On success: toast ("Member added" / "Member updated"), close, refresh.
- `400`: `GuestName` errors shown under Name; any others as a toast.
- Deletion is done from the member card, not from this modal.

---

## Screen 4 — Add / Edit Expense (modal)

**Component:** `src/components/add-expense-modal.jsx`
**API calls:** Add `POST /api/groups/{guestToken}/expenses` · Edit `PUT /api/groups/{guestToken}/expenses/{expenseId}`

```
┌─────────────────────────────────────────────┐
│  Add Expense                         [ × ]  │
├─────────────────────────────────────────────┤
│  Title         [____________________________]│
│  Total Amount  LKR [_________________________]│
│                                             │
│  Who Paid?                 Split the payment│
│  (Alice ✓) (Bob) (Carol)                    │   single-payer mode
│                                             │
│  Split Between        Select All · None     │
│  (Alice ✓) (Bob ✓) (Carol ✓)                │
│                                             │
│  [ ⊕ Add Expense ]                          │   (edit mode: "Save Changes")
└─────────────────────────────────────────────┘

  multi-payer mode ("Split the payment"):
  ┌─────────────────────────────────────────┐
  │ [✓] Alice              LKR [ 20000.00 ] │
  │ [✓] Bob                LKR [ 10000.00 ] │
  │ [ ] Carol                               │
  └─────────────────────────────────────────┘
  ✓ Total allocated: LKR 30,000.00 / LKR 30,000.00
```

### Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| Title | Text input | Yes | API max 150 chars |
| Total Amount | Number input (step 0.01) | Yes | > 0, at most 2 decimals (API) |
| Who Paid? | Single-payer chips, or multi-payer checkbox list with an amount per selected payer | Yes | In single-payer mode the payer's amount always equals the total |
| Split Between | Member chips | Yes (≥ 1) | All members selected by default. Sent as `participantMemberIds` |

### Behaviour

- **Add mode:** empty, no payer selected, all members in the split.
- **Edit mode:** pre-filled from the expense; multi-payer mode if it has more than one payer;
  participants from `splits`. The full body is always sent on `PUT`.
- Multi-payer mode shows a live "Total allocated: X / Y" line — green ✓ when the payer amounts
  equal the total to the cent, red ⚠ otherwise.
- Switching back to single-payer keeps only the first selected payer.
- The submit button is always enabled; validation runs on submit:
  title required · amount > 0 · at least one payer · payer amounts sum exactly to the total
  (compared in whole cents, matching the API) · at least one participant.
- `400`: `Title`, `TotalAmount`, `Payers` / `Payers[i].*` and `ParticipantMemberIds` errors are
  shown inline under their field; any others as a toast.

---

## Shared UX Patterns

### Shareable link

The `guestToken` in the URL is the only thing needed to re-open a group on any device. The
"Copy Link" button in the group header copies the current URL. Anyone with the link has full
read/write access.

### Loading & error states

| State | Handling |
|---|---|
| Loading the group | Full-page spinner; summary sidebar shows skeleton cards |
| 404 on group load | Full-page "Group not found" — "This group link may be invalid or the group was deleted." + Back to Home |
| Other load failure | Full-page "Something went wrong" + Retry (reloads), plus an error toast |
| 400 validation errors | Inline under the matching field; unmatched messages as a toast |
| 409 on member delete | Error toast |
| 500 / network error on an action | Error toast with the API's `title` (e.g. "An unexpected error occurred.") or the browser's fetch error |

### Confirmation prompts

Required before destructive actions:
- Deleting an expense: `Delete "{title}"?` — "This expense will be removed from the group. This action cannot be undone."
- Deleting a member: `Delete {name}?` — "This member will be removed from the group. This action cannot be undone."

---

## Not implemented

| Item | Status |
|---|---|
| "Settle Group Now" / archive group | UI stub only — no API endpoint |
| Phase 2 UI (register, login, profile, my groups) | API exists; no frontend |
| "Added by" (`createdByMemberId`) | Deferred to Phase 2 |
| Uneven splits (shares, exact amounts, percentages) | Not supported — every expense is split equally among its participants |

---

*Covers Phase 1 guest APIs only — no login required. Last updated: 2026-11-14.*
