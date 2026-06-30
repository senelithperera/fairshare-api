using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;

namespace FairShare.Application.UseCases.Expenses;

/// <summary>Updates an existing expense and recalculates its splits.</summary>
public class UpdateExpenseUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IExpenseRepository _expenses;

    public UpdateExpenseUseCase(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IExpenseRepository expenses)
    {
        _groups   = groups;
        _members  = members;
        _expenses = expenses;
    }

    /// <summary>Executes the use case.</summary>
    /// <returns>Updated expense response, or null when the group or expense is not found.</returns>
    public async Task<ExpenseResponse?> ExecuteAsync(
        Guid guestToken,
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return null;

        var expense = await _expenses.GetByIdAsync(expenseId, ct);
        if (expense is null || expense.GroupId != group.Id) return null;

        var allMembers = await _members.GetByGroupIdAsync(group.Id, ct);

        // Apply patches
        if (request.Title is not null)
            expense.Title = request.Title.Trim();

        if (request.TotalAmount.HasValue)
            expense.TotalAmount = request.TotalAmount.Value;

        if (request.Payers is not null)
        {
            _expenses.RemovePayers(expense.Payers.ToList());
            expense.Payers = request.Payers.Select(p => new ExpensePayer
            {
                Id         = Guid.NewGuid(),
                ExpenseId  = expense.Id,
                MemberId   = Guid.Parse(p.MemberId),
                AmountPaid = p.AmountPaid
            }).ToList();
        }

        // Recalculate splits
        _expenses.RemoveSplits(expense.Splits.ToList());
        expense.Splits = BuildExpenseSplits(expense.Id, allMembers, expense.TotalAmount);

        _expenses.Update(expense);
        await _expenses.SaveChangesAsync(ct);

        return MapToResponse(expense, allMembers);
    }

    private static List<ExpenseSplit> BuildExpenseSplits(
        Guid expenseId,
        IReadOnlyList<GroupMember> members,
        decimal totalAmount)
    {
        decimal equalShare = totalAmount / members.Count;
        decimal remainder  = totalAmount - equalShare * members.Count;

        return members.Select((m, i) => new ExpenseSplit
        {
            Id          = Guid.NewGuid(),
            ExpenseId   = expenseId,
            MemberId    = m.Id,
            ShareAmount = i == 0 ? equalShare + remainder : equalShare
        }).ToList();
    }

    private static ExpenseResponse MapToResponse(Expense e, IReadOnlyList<GroupMember> members)
    {
        var memberLookup = members.ToDictionary(m => m.Id, m => m.GuestName ?? m.User?.Name ?? "Unknown");

        return new ExpenseResponse(
            e.Id.ToString(),
            e.GroupId.ToString(),
            e.Title,
            e.TotalAmount,
            e.CreatedAt.ToString("o"),
            e.Payers.Select(p => new ExpensePayerResponse(
                p.MemberId.ToString(),
                memberLookup.GetValueOrDefault(p.MemberId, "Unknown"),
                p.AmountPaid)).ToList(),
            e.Splits.Select(s => new ExpenseSplitResponse(
                s.MemberId.ToString(),
                memberLookup.GetValueOrDefault(s.MemberId, "Unknown"),
                s.ShareAmount)).ToList());
    }
}
