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

        var memberInputs = allMembers.Select(m => new MemberInput(
            m.Id,
            m.GuestName ?? m.User?.Name ?? "Unknown",
            m.BudgetCap,
            paidPerMember.GetValueOrDefault(m.Id, 0m))).ToList();

        var result = SplitCalculator.Calculate(memberInputs, totalSpent);

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
                s.Amount)).ToList());
    }
}
