using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Domain.Services;

namespace FairShare.Application.UseCases.Expenses;

/// <summary>Runs the <see cref="SplitCalculator"/> over all active group expenses and returns the summary.</summary>
public class GetSummaryUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IExpenseRepository _expenses;

    public GetSummaryUseCase(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IExpenseRepository expenses)
    {
        _groups   = groups;
        _members  = members;
        _expenses = expenses;
    }

    /// <summary>Executes the use case.</summary>
    /// <returns>Summary response, or null when the group is not found.</returns>
    public async Task<SummaryResponse?> ExecuteAsync(Guid guestToken, CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return null;

        var allMembers  = await _members.GetByGroupIdAsync(group.Id, ct);
        var allExpenses = await _expenses.GetByGroupIdAsync(group.Id, ct);

        decimal totalSpent = allExpenses.Sum(e => e.TotalAmount);

        // Build aggregate paid amounts per member across all expenses
        var paidPerMember = allExpenses
            .SelectMany(e => e.Payers)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));

        // Build each member's real fair share from the actual persisted splits — this is
        // what makes per-expense participant exclusion affect balances, since a member who
        // was excluded from an expense simply has no ExpenseSplit row for it.
        var sharePerMember = allExpenses
            .SelectMany(e => e.Splits)
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.ShareAmount));

        var memberNames = allMembers.ToDictionary(m => m.Id, m => m.GuestName ?? m.User?.Name ?? "Unknown");

        var memberInputs = allMembers.Select(m => new MemberInput(
            m.Id,
            memberNames[m.Id],
            m.BudgetCap,
            paidPerMember.GetValueOrDefault(m.Id, 0m),
            sharePerMember.GetValueOrDefault(m.Id, 0m))).ToList();

        var result = SplitCalculator.Calculate(memberInputs);

        // Detailed view: settlements traced back to each individual expense (no cross-expense
        // netting), so every pairing shown is between people who actually shared that expense —
        // unlike the group-wide `result.Settlements` above, which may pair up two people who
        // never split anything together, purely to minimize the total number of transactions.
        var detailedSettlements = new List<ExpenseSettlementGroupResponse>();
        foreach (var expense in allExpenses)
        {
            var paidLookup  = expense.Payers.GroupBy(p => p.MemberId).ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));
            var shareLookup = expense.Splits.ToDictionary(s => s.MemberId, s => s.ShareAmount);

            var expenseMemberIds = expense.Payers.Select(p => p.MemberId)
                .Concat(expense.Splits.Select(s => s.MemberId))
                .Distinct();

            var expenseInputs = expenseMemberIds.Select(id => new ExpenseMemberInput(
                id,
                memberNames.GetValueOrDefault(id, "Unknown"),
                paidLookup.GetValueOrDefault(id, 0m),
                shareLookup.GetValueOrDefault(id, 0m))).ToList();

            var expenseSettlements = SplitCalculator.CalculateExpenseSettlements(expenseInputs);
            if (expenseSettlements.Count == 0) continue;

            detailedSettlements.Add(new ExpenseSettlementGroupResponse(
                expense.Id.ToString(),
                expense.Title,
                expenseSettlements.Select(s => new SettlementResponse(
                    s.FromMemberId.ToString(),
                    s.FromName,
                    s.ToMemberId.ToString(),
                    s.ToName,
                    s.Amount)).ToList()));
        }

        return new SummaryResponse(
            totalSpent,
            result.MemberBalances.Select(b => new MemberBalanceResponse(
                b.MemberId.ToString(),
                b.Name,
                Math.Round(b.Balance, 2),
                b.IsCreditor)).ToList(),
            result.Settlements.Select(s => new SettlementResponse(
                s.FromMemberId.ToString(),
                s.FromName,
                s.ToMemberId.ToString(),
                s.ToName,
                s.Amount)).ToList(),
            detailedSettlements);
    }
}
