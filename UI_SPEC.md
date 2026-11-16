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
│                 [▣]                      │
│              FairShare                   │
│  Split shared costs with your group and  │
│  see exactly who pays whom. No sign-up.  │
│                                          │
│  ┌ Create New Group ───────────────────┐ │
│  │ Group Name  [______________________]│ │
│  │ Currency    [ LKR ▾ ]               │ │
│  │          [ ⊕ Create Group ]         │ │
│  │ ⓘ You'll get a link to share…       │ │
│  └─────────────────────────────────────┘ │
│              HOW IT WORKS                │
│  (1) Add people  (2) Add expenses        │
│  (3) See who pays whom                   │
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
- Below the form, "How it works" lists the three steps (an `<ol>`; one column on phones, three
  from `sm`): Add people · Add expenses · See who pays whom, each with one line of detail. The
  copy lives in `src/lib/steps.js` and is reused by the group page's empty states, so a new
  group follows the same steps the home page described.
- The card footer explains that anyone with the group's link can view and edit it.
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
│ Members                  [+ Add Member]   │ ┌ Group Total Spent ───┐ │
│                                           │ │ LKR 90,000.00        │ │
│ ┌───────────────────┐ ┌───────────────────┐│ └──────────────────────┘ │
│ │(A) Alice    ✎ 🗑  │ │(B) Bob      ✎ 🗑  ││ ┌ Balances ────────────┐ │
│ │Paid 30,000 · 2 exp│ │Paid 0.00 · 1 exp  ││ │ Alice  + LKR 60,000  │ │
│                                           │ │ Bob    - LKR 30,000  │ │
│ Expenses                   [+ Add Expense]│ │ Carol  - LKR 30,000  │ │
│ ┌───────────────────────────────────────┐ │ └──────────────────────┘ │
│ │ 🏨 Hotel            LKR 30,000.00 ✎ 🗑 │ │ ┌ Who pays whom ──────┐ │
│ │    Paid by Alice                      │ │ │ (Simplified|Detailed)│ │
│ │    👥 Split between Alice and Bob ·    │ │
│ │       LKR 15,000.00 each              │ │ │ Bob → Alice  30,000  │ │
│ └───────────────────────────────────────┘ │ │ Carol → Alice 30,000 │ │
│                                           │ │ 2 payments make      │ │
│                                           │ │ everyone even        │ │
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

- "Add Member" sits in the section header and opens the member modal (Screen 3).
- One `MemberCard` per member (two per row from `sm` up): avatar (initials) + `displayName`,
  a second line "Paid {totalAmountPaid} · N expenses" (or "Not in any expenses yet"), and
  always-visible 44px ✎ (edit) and 🗑 (delete) buttons.
- 🗑 when the member is a payer or participant in any expense (worked out from the loaded
  group) → info dialog "{name} can't be removed yet" naming those expenses and how to free the
  member. No API call.
- 🗑 otherwise → confirm dialog "Delete {name}?" → `DELETE …/members/{memberId}`.
  - `204` → toast "Member removed".
  - `409` (someone added them to an expense meanwhile) → error toast.
- Empty (step 1 of 3): dashed "Step 1 of 3 · Add the people in this group" slot with a filled
  "Add first member" button. The header button is hidden until there is a member.

### Expenses section

- "Add Expense" (filled) sits in the section header once there is at least one expense.
- Button styles (`src/components/button.js`): only the next step is filled (**primary**) — "Add
  first member", then "Add first expense", then the header "Add Expense". "Add Member" is
  outlined (**secondary**) once members exist; Copy Link is **tonal**. No button is ever disabled with a hover-only explanation.
- On phones (below `lg`), once there are expenses, a "Who pays whom · N payments" bar under the
  header scrolls to the settlements card and moves focus to its heading.
- One `ExpenseCard` per expense: icon (guessed from keywords in the title), `title`,
  "Paid by {payer names}", `totalAmount`, and always a sharing line built from `splits`:
  "Split equally between all 3 · LKR 10,000.00 each", "Split between Alice and Bob · LKR
  15,000.00 each" (names after two summarised as "and N others"), or "For Alice only". When
  shares differ by a cent it reads "about LKR 33.33 each".
- ✎ opens the expense modal pre-filled (Screen 4).
- 🗑 → confirm dialog `Delete "{title}"?` → `DELETE …/expenses/{expenseId}` (hard delete, no undo)
  → toast "Expense removed".
- Empty, no members yet (step 2 of 3, greyed): "Then add what was paid — Once someone is in the
  group, you can add expenses here." No button.
- Empty with members (step 2 of 3): "Add the first expense" with a filled "Add first expense"
  button.

### Summary sidebar — `src/components/summary-sidebar.jsx`

| Card | Source | Display |
|---|---|---|
| Group Total Spent | `totalSpent` | Formatted with currency |
| Balances | `memberBalances[]` | Avatar, name and a plain-language status, compared in whole cents: green "gets back {amount}", red "owes {amount}", or grey "all square" for zero |
| Who pays whom | `settlements[]` / `detailedSettlements[]` | Toggle: **Simplified** = group-wide netted transfers; **Detailed** = transfers grouped under each expense title. "From → To  amount" per row. Under the heading: "The fewest payments that make everyone even. Pay each other directly; FairShare works out the payments but doesn't send or track money." Above each list a count line: Simplified "N payments make everyone even"; Detailed "N payments, expense by expense · M more than Simplified". No payments → "Everyone is even · No payments needed" (check icon). There is no settle or "mark as paid" action in version 1. With no expenses at all the card instead shows a dashed "Step 3 of 3" slot ("Once expenses are added, this shows the fewest payments that make everyone even.") and hides the toggle. Each view shows a one-line hint above the list: Simplified explains that debts are combined across expenses (so you may pay someone you didn't share an expense with, for the same total) with a "See it per expense" link to Detailed; Detailed explains that you only pay people you shared each expense with, which can mean more payments |

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
- "Who Paid?" and "Split Between" each have a one-line helper ("The person who paid the bill." /
  "Everyone who shares the cost. Each person pays an equal part."). The multi-payer switch reads
  "More than one person paid?" (and "Only one person paid" to switch back).
- Under the participant chips a live preview shows each share, using the API's whole-cent rule:
  "LKR 10,000.00 each · 3 people", or "LKR 33.33 to LKR 33.34 each · 3 people" when cents don't
  divide evenly; prompts instead when the total or participants are missing.
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
| Settling, "mark as paid", archive group | Out of scope for version 1 — payments happen outside the app. The "Settle Group Now" stub was removed |
| Phase 2 UI (register, login, profile, my groups) | API exists; no frontend |
| "Added by" (`createdByMemberId`) | Deferred to Phase 2 |
| Uneven splits (shares, exact amounts, percentages) | Not supported — every expense is split equally among its participants |

---

*Covers Phase 1 guest APIs only — no login required. Last updated: 2026-11-14.*
