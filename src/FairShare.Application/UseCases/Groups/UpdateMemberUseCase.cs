using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Updates a guest member's name or budget cap.</summary>
public class UpdateMemberUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;

    public UpdateMemberUseCase(IGroupRepository groups, IGroupMemberRepository members)
    {
        _groups  = groups;
        _members = members;
    }

    /// <summary>
    /// Executes the use case.
    /// </summary>
    /// <returns>
    /// Updated member response, <c>null</c> when the group or member is not found,
    /// or throws <see cref="InvalidOperationException"/> when the member does not belong to the group.
    /// </returns>
    public async Task<GroupMemberResponse?> ExecuteAsync(
        Guid guestToken,
        Guid memberId,
        UpdateMemberRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return null;

        var member = await _members.GetByIdAsync(memberId, ct);
        if (member is null) return null;

        if (member.GroupId != group.Id)
            throw new InvalidOperationException("Member does not belong to this group.");

        if (request.GuestName is not null)
            member.GuestName = request.GuestName.Trim();

        if (request.BudgetCap.HasValue)
            member.BudgetCap = request.BudgetCap;

        _members.Update(member);
        await _members.SaveChangesAsync(ct);

        decimal totalPaid = member.PayerRecords.Sum(p => p.AmountPaid);

        return new GroupMemberResponse(
            member.Id.ToString(),
            member.GuestName ?? member.User?.Name ?? "Unknown",
            member.BudgetCap,
            totalPaid,
            member.JoinedAt.ToString("o"));
    }
}
