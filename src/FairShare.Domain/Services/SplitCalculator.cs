namespace FairShare.Domain.Services;

/// <summary>
/// Pure domain service that computes member balances and minimal settlements for a group.
/// Has zero infrastructure dependencies and is fully unit-testable.
/// </summary>
public static class SplitCalculator
{
    /// <summary>
    /// Calculates the fair share each member owes, their resulting balance, and the
    /// minimum set of cash transfers needed to settle all debts within the group.
    /// </summary>
    /// <param name="members">Snapshot of group members with their caps and total amounts paid.</param>
    /// <param name="totalSpent">Sum of all non-deleted expense amounts in the group.</param>
    /// <returns>
    /// A <see cref="SplitResult"/> containing per-member balances and a simplified settlement list.
    /// </returns>
    public static SplitResult Calculate(IReadOnlyList<MemberInput> members, decimal totalSpent)
    {
        if (members.Count == 0)
            return new SplitResult([], []);

        // STEP 1 — trivial if nothing was spent
        if (totalSpent == 0)
        {
            var zeroBalances = members
                .Select(m => new MemberBalance(m.MemberId, m.Name, 0m, false))
                .ToList();
            return new SplitResult(zeroBalances, []);
        }

        // STEP 2 — partition members
        var cappedMembers   = members.Where(m => m.BudgetCap.HasValue).ToList();
        var uncappedMembers = members.Where(m => !m.BudgetCap.HasValue).ToList();

        // STEP 3 — sum of all caps
        decimal cappedTotal = cappedMembers.Sum(m => m.BudgetCap!.Value);

        // STEP 4 — fall back to full equal split if caps >= total
        bool useCaps = cappedTotal < totalSpent && uncappedMembers.Count > 0;

        Dictionary<Guid, decimal> fairShare = new();

        if (!useCaps)
        {
            // Equal split across all members
            decimal equalShare = totalSpent / members.Count;
            decimal remainder  = totalSpent - equalShare * members.Count;

            bool remainderAssigned = false;
            foreach (var m in members)
            {
                decimal share = equalShare;
                if (!remainderAssigned && remainder != 0)
                {
                    share += remainder;
                    remainderAssigned = true;
                }
                fairShare[m.MemberId] = share;
            }
        }
        else
        {
            // STEP 5 — equal share for uncapped members
            decimal remaining  = totalSpent - cappedTotal;
            decimal equalShare = remaining / uncappedMembers.Count;
            decimal remainder  = remaining - equalShare * uncappedMembers.Count;

            // STEP 6 — assign shares
            foreach (var m in cappedMembers)
                fairShare[m.MemberId] = m.BudgetCap!.Value;

            bool remainderAssigned = false;
            foreach (var m in uncappedMembers)
            {
                decimal share = equalShare;
                if (!remainderAssigned && remainder != 0)
                {
                    share += remainder;
                    remainderAssigned = true;
                }
                fairShare[m.MemberId] = share;
            }
        }

        // STEP 7 — balance per member (positive = creditor, negative = debtor)
        var balances = members
            .Select(m => new MemberBalance(
                m.MemberId,
                m.Name,
                m.TotalAmountPaid - fairShare[m.MemberId],
                m.TotalAmountPaid - fairShare[m.MemberId] > 0))
            .ToList();

        // STEP 8 — greedy debt simplification
        var settlements = SimplifyDebts(balances);

        return new SplitResult(balances, settlements);
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

            creditorsWork[ci] = (creditor.MemberId, creditor.Name, creditor.Balance - amount);
            debtorsWork[di]   = (debtor.MemberId,   debtor.Name,   debtor.Balance   - amount);

            if (creditorsWork[ci].Balance <= 0) ci++;
            if (debtorsWork[di].Balance   <= 0) di++;
        }

        return settlements;
    }
}

/// <summary>Input record for a group member passed into <see cref="SplitCalculator"/>.</summary>
/// <param name="MemberId">Unique identifier of the group member.</param>
/// <param name="Name">Display name.</param>
/// <param name="BudgetCap">Optional spending cap. Null means uncapped (equal split).</param>
/// <param name="TotalAmountPaid">Sum of all amounts this member has paid across all expenses.</param>
public record MemberInput(Guid MemberId, string Name, decimal? BudgetCap, decimal TotalAmountPaid);

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
