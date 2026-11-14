using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Domain.Services;

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

        // Null means "keep the expense's current participants" (mirrors the partial-update
        // convention used for the other optional fields below).
        var participantIds = request.ParticipantMemberIds
            ?? expense.Splits.Select(s => s.MemberId.ToString()).ToList();
        var participants = ResolveParticipants(participantIds, allMembers);

        // Apply patches
        if (request.Title is not null)
            expense.Title = request.Title.Trim();

        if (request.TotalAmount.HasValue)
            expense.TotalAmount = request.TotalAmount.Value;

        if (request.Payers is not null)
        {
            _expenses.RemovePayers(expense.Payers.ToList());
            var newPayers = request.Payers.Select(p => new ExpensePayer
            {
                Id         = Guid.NewGuid(),
                ExpenseId  = expense.Id,
                MemberId   = Guid.Parse(p.MemberId),
                AmountPaid = p.AmountPaid
            }).ToList();
            // Explicitly track the new rows as Added: `expense` is already tracked, and simply
            // reassigning its Payers navigation to a list of brand-new entities can leave EF's
            // change tracker treating them as Modified (since their client-generated GUID keys
            // are never "empty"), producing UPDATE statements for rows that don't exist yet.
            _expenses.AddPayers(newPayers);
            expense.Payers = newPayers;
        }

        // Recalculate splits — same explicit-tracking reasoning as the payers above.
        _expenses.RemoveSplits(expense.Splits.ToList());
        var newSplits = BuildExpenseSplits(expense.Id, participants, expense.TotalAmount);
        _expenses.AddSplits(newSplits);
        expense.Splits = newSplits;

        await _expenses.SaveChangesAsync(ct);

        return MapToResponse(expense, allMembers);
    }

    /// <summary>
    /// Resolves which members participate in the split, falling back to everyone in the
    /// group if the supplied ids don't match any current member.
    /// </summary>
    private static IReadOnlyList<GroupMember> ResolveParticipants(
        IReadOnlyList<string> participantMemberIds,
        IReadOnlyList<GroupMember> allMembers)
    {
        var filtered = allMembers.Where(m => participantMemberIds.Contains(m.Id.ToString())).ToList();
        return filtered.Count > 0 ? filtered : allMembers;
    }

    private static List<ExpenseSplit> BuildExpenseSplits(
        Guid expenseId,
        IReadOnlyList<GroupMember> members,
        decimal totalAmount)
    {
        var shares = SplitCalculator.SplitEvenly(totalAmount, members.Count);

        return members.Select((m, i) => new ExpenseSplit
        {
            Id          = Guid.NewGuid(),
            ExpenseId   = expenseId,
            MemberId    = m.Id,
            ShareAmount = shares[i]
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
