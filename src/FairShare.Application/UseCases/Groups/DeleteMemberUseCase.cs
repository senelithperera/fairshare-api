using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Result codes for the delete-member operation.</summary>
public enum DeleteMemberResult
{
    Success,
    GroupNotFound,
    MemberNotFound,
    MemberHasExpenses
}

/// <summary>Removes a guest member from a group, provided they have no associated expenses.</summary>
public class DeleteMemberUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;

    public DeleteMemberUseCase(IGroupRepository groups, IGroupMemberRepository members)
    {
        _groups  = groups;
        _members = members;
    }

    /// <summary>Executes the use case.</summary>
    public async Task<DeleteMemberResult> ExecuteAsync(
        Guid guestToken,
        Guid memberId,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return DeleteMemberResult.GroupNotFound;

        var member = await _members.GetByIdAsync(memberId, ct);
        if (member is null || member.GroupId != group.Id)
            return DeleteMemberResult.MemberNotFound;

        if (await _members.HasExpensesAsync(memberId, ct))
            return DeleteMemberResult.MemberHasExpenses;

        _members.Remove(member);
        await _members.SaveChangesAsync(ct);

        return DeleteMemberResult.Success;
    }
}
