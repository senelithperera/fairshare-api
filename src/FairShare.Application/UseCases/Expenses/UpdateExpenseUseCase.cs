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
    /// <returns>The updated expense, or NotFound / PayerNotInGroup.</returns>
    public async Task<ExpenseResult> ExecuteAsync(
        Guid guestToken,
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return ExpenseResult.NotFound;

        var expense = await _expenses.GetByIdAsync(expenseId, ct);
        if (expense is null || expense.GroupId != group.Id) return ExpenseResult.NotFound;

        var allMembers = await _members.GetByGroupIdAsync(group.Id, ct);

        // Member ids are well-formed GUIDs here (enforced by the validator).
        if (request.Payers is not null &&
            request.Payers.Any(p => allMembers.All(m => m.Id != Guid.Parse(p.MemberId))))
            return ExpenseResult.PayerNotInGroup;

        // Null means "keep the expense's current participants" (mirrors the partial-update
        // convention used for the other optional fields below).
        var participantIds = request.ParticipantMemberIds
            ?? expense.Splits.Select(s => s.MemberId.ToString()).ToList();
        var participants = ResolveParticipants(participantIds, allMembers);
        if (participants is null) return ExpenseResult.ParticipantNotInGroup;

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

        return ExpenseResult.Success(MapToResponse(expense, allMembers));
    }

    /// <summary>
    /// Resolves the supplied ids to group members (in join order), or null if any id isn't a
    /// current member of the group. Ids are well-formed GUIDs here (enforced by the validator).
    /// </summary>
    private static IReadOnlyList<GroupMember>? ResolveParticipants(
        IReadOnlyList<string> participantMemberIds,
        IReadOnlyList<GroupMember> allMembers)
    {
        var ids = participantMemberIds.Select(Guid.Parse).ToHashSet();
        var participants = allMembers.Where(m => ids.Contains(m.Id)).ToList();
        return participants.Count == ids.Count ? participants : null;
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
