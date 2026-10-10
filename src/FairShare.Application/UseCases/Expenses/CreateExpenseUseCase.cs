using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Expenses;

/// <summary>Adds an expense to a group, calculates splits, and persists all records.</summary>
public class CreateExpenseUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IExpenseRepository _expenses;
    private readonly ILogger<CreateExpenseUseCase> _logger;

    public CreateExpenseUseCase(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IExpenseRepository expenses,
        ILogger<CreateExpenseUseCase> logger)
    {
        _groups   = groups;
        _members  = members;
        _expenses = expenses;
        _logger   = logger;
    }

    /// <summary>Executes the use case.</summary>
    /// <param name="guestToken">Group guest token.</param>
    /// <param name="request">Expense details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created expense, or NotFound / PayerNotInGroup.</returns>
    public async Task<ExpenseResult> ExecuteAsync(
        Guid guestToken,
        CreateExpenseRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return ExpenseResult.NotFound;

        var allMembers = await _members.GetByGroupIdAsync(group.Id, ct);

        // Member ids are well-formed GUIDs here (enforced by the validator).
        if (request.Payers.Any(p => allMembers.All(m => m.Id != Guid.Parse(p.MemberId))))
            return ExpenseResult.PayerNotInGroup;

        // Null means "everyone in the group".
        var participants = request.ParticipantMemberIds is null
            ? allMembers
            : ResolveParticipants(request.ParticipantMemberIds, allMembers);
        if (participants is null) return ExpenseResult.ParticipantNotInGroup;

        var payers = request.Payers.Select(p => new ExpensePayer
        {
            Id         = Guid.NewGuid(),
            MemberId   = Guid.Parse(p.MemberId),
            AmountPaid = p.AmountPaid
        }).ToList();

        var expense = new Expense
        {
            Id                = Guid.NewGuid(),
            GroupId           = group.Id,
            Title             = request.Title.Trim(),
            TotalAmount       = request.TotalAmount,
            CreatedByMemberId = null,
            CreatedAt         = DateTime.UtcNow,
            Payers            = payers,
            Splits            = BuildExpenseSplits(Guid.Empty, participants, request.TotalAmount)
        };

        // Fix up the ExpenseId on splits now that we have it
        foreach (var s in expense.Splits)
            s.ExpenseId = expense.Id;

        await _expenses.AddAsync(expense, ct);
        await _expenses.SaveChangesAsync(ct);

        _logger.LogInformation("Created expense {ExpenseId} in group {GroupId}", expense.Id, group.Id);

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
