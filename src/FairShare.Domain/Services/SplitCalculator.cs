namespace FairShare.Domain.Services;

/// <summary>
/// Pure domain service that computes member balances and minimal settlements for a group.
/// Has zero infrastructure dependencies and is fully unit-testable.
/// </summary>
public static class SplitCalculator
{
    /// <summary>
    /// Calculates each member's balance (amount paid minus fair share) and the minimum set of
    /// cash transfers needed to settle all debts within the group.
    /// </summary>
    /// <param name="members">
    /// Snapshot of group members with their total amounts paid and fair share (the sum of their
    /// real per-expense split amounts — already respects which expenses each member actually
    /// participated in).
    /// </param>
    /// <returns>
    /// A <see cref="SplitResult"/> containing per-member balances and a simplified settlement list.
    /// </returns>
    public static SplitResult Calculate(IReadOnlyList<MemberInput> members)
    {
        if (members.Count == 0)
            return new SplitResult([], []);

        // Balance per member (positive = creditor, negative = debtor)
        var balances = members
            .Select(m => new MemberBalance(
                m.MemberId,
                m.Name,
                m.TotalAmountPaid - m.FairShare,
                m.TotalAmountPaid - m.FairShare > 0))
            .ToList();

        // Greedy debt simplification
        var settlements = SimplifyDebts(balances);

        return new SplitResult(balances, settlements);
    }

    /// <summary>
    /// Splits <paramref name="total"/> into <paramref name="count"/> shares of whole cents that
    /// always sum exactly to the total. Each share is the total divided evenly and rounded down
    /// to the cent; the leftover cents are then handed out one each to the first members, so no
    /// two shares differ by more than 0.01 (e.g. 100 / 3 → 33.34, 33.33, 33.33).
    /// </summary>
    /// <remarks>
    /// Rounding must happen here rather than being left to the <c>numeric(18,2)</c> column —
    /// otherwise every share gets rounded independently on insert and the stored shares drift
    /// away from the total (100 / 3 would persist as 33.33 × 3 = 99.99).
    /// </remarks>
    public static IReadOnlyList<decimal> SplitEvenly(decimal total, int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Cannot split across zero members.");

        // Match how Postgres stores the total, so shares sum to the persisted value.
        total = Math.Round(total, 2, MidpointRounding.AwayFromZero);

        decimal baseShare  = Math.Floor(total * 100 / count) / 100m;
        int leftoverCents  = (int)((total - baseShare * count) * 100);

        return Enumerable.Range(0, count)
            .Select(i => baseShare + (i < leftoverCents ? 0.01m : 0m))
            .ToList();
    }

    /// <summary>
    /// Computes settlements scoped to a single expense — who owes whom based only on that
    /// expense's own payers and split shares, with no cross-expense netting. Unlike
    /// <see cref="Calculate"/>, every pairing here is directly traceable back to this one
    /// expense (useful for a "detailed" settlement view, as opposed to the group-wide
    /// minimal-transaction view where a pairing may involve two people who never actually
    /// shared an expense together).
    /// </summary>
    /// <param name="members">
    /// Each participant's amount paid and share owed for this single expense (0 for either
    /// if they didn't pay/participate).
    /// </param>
    public static IReadOnlyList<Settlement> CalculateExpenseSettlements(
        IReadOnlyList<ExpenseMemberInput> members)
    {
        var balances = members
            .Select(m => new MemberBalance(
                m.MemberId,
                m.Name,
                m.AmountPaid - m.ShareAmount,
                m.AmountPaid - m.ShareAmount > 0))
            .ToList();

        return SimplifyDebts(balances);
    }

    /// <summary>
    /// Greedy min-transaction algorithm: repeatedly match the largest creditor with the largest debtor
    /// until all balances are settled.
    /// </summary>
    private static List<Settlement> SimplifyDebts(List<MemberBalance> balances)
    {
        // Use mutable working copies, ignore zero-balance members
        var creditors = balances
            .Where(b => b.Balance > 0)
            .Select(b => (b.MemberId, b.Name, Balance: b.Balance))
            .OrderByDescending(b => b.Balance)
            .ToList();

        var debtors = balances
            .Where(b => b.Balance < 0)
            .Select(b => (b.MemberId, b.Name, Balance: Math.Abs(b.Balance)))
            .OrderByDescending(b => b.Balance)
            .ToList();

        var settlements = new List<Settlement>();

        int ci = 0, di = 0;
        // Rebuild as mutable structs via ValueTuple lists
        var creditorsWork = creditors.Select(c => (c.MemberId, c.Name, c.Balance)).ToList();
        var debtorsWork   = debtors.Select(d => (d.MemberId, d.Name, d.Balance)).ToList();

        while (ci < creditorsWork.Count && di < debtorsWork.Count)
        {
            var creditor = creditorsWork[ci];
            var debtor   = debtorsWork[di];

            decimal amount = Math.Min(creditor.Balance, debtor.Balance);
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

            if (amount > 0)
            {
                settlements.Add(new Settlement(
                    debtor.MemberId,
                    debtor.Name,
                    creditor.MemberId,
                    creditor.Name,
                    amount));
            }

            decimal remainingCreditor = creditor.Balance - amount;
            decimal remainingDebtor   = debtor.Balance   - amount;

            // Rounding the transferred amount to whole cents can leave a residue smaller
            // than $0.01 that would never cross zero (and would spin this loop forever) —
            // currency can't represent it, so treat it as settled.
            if (Math.Abs(remainingCreditor) < 0.01m) remainingCreditor = 0m;
            if (Math.Abs(remainingDebtor)   < 0.01m) remainingDebtor   = 0m;

            creditorsWork[ci] = (creditor.MemberId, creditor.Name, remainingCreditor);
            debtorsWork[di]   = (debtor.MemberId,   debtor.Name,   remainingDebtor);

            if (creditorsWork[ci].Balance <= 0) ci++;
            if (debtorsWork[di].Balance   <= 0) di++;
        }

        return settlements;
    }
}

/// <summary>Input record for a group member passed into <see cref="SplitCalculator"/>.</summary>
/// <param name="MemberId">Unique identifier of the group member.</param>
/// <param name="Name">Display name.</param>
/// <param name="TotalAmountPaid">Sum of all amounts this member has paid across all expenses.</param>
/// <param name="FairShare">
/// Sum of this member's real per-expense split amounts (<c>ExpenseSplit.ShareAmount</c>)
/// across all expenses. Naturally excludes expenses the member didn't participate in.
/// </param>
public record MemberInput(Guid MemberId, string Name, decimal TotalAmountPaid, decimal FairShare);

/// <summary>Input record for <see cref="SplitCalculator.CalculateExpenseSettlements"/>.</summary>
/// <param name="MemberId">Unique identifier of the group member.</param>
/// <param name="Name">Display name.</param>
/// <param name="AmountPaid">Amount this member paid toward this specific expense (0 if not a payer).</param>
/// <param name="ShareAmount">Amount this member owes for this specific expense (0 if excluded).</param>
public record ExpenseMemberInput(Guid MemberId, string Name, decimal AmountPaid, decimal ShareAmount);

/// <summary>Computed balance result for a single member.</summary>
/// <param name="MemberId">Unique identifier of the group member.</param>
/// <param name="Name">Display name.</param>
/// <param name="Balance">
/// Positive → creditor (is owed money).
/// Negative → debtor (owes money).
/// Zero → settled.
/// </param>
/// <param name="IsCreditor">True when the member is owed money.</param>
public record MemberBalance(Guid MemberId, string Name, decimal Balance, bool IsCreditor);

/// <summary>A single cash transfer needed to settle debts.</summary>
/// <param name="FromMemberId">ID of the member who must pay.</param>
/// <param name="FromName">Name of the member who must pay.</param>
/// <param name="ToMemberId">ID of the member who receives the payment.</param>
/// <param name="ToName">Name of the member who receives the payment.</param>
/// <param name="Amount">Amount to transfer in the group's currency.</param>
public record Settlement(Guid FromMemberId, string FromName, Guid ToMemberId, string ToName, decimal Amount);

/// <summary>Output of <see cref="SplitCalculator.Calculate"/>.</summary>
/// <param name="MemberBalances">Per-member balance information.</param>
/// <param name="Settlements">Minimal list of transfers to settle all debts.</param>
public record SplitResult(IReadOnlyList<MemberBalance> MemberBalances, IReadOnlyList<Settlement> Settlements);
